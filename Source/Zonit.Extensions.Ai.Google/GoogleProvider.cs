using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Ai.Converters;

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Google Gemini provider implementation.
/// </summary>
[AiProvider("google")]
public sealed class GoogleProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleProvider> _logger;
    private readonly GoogleOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GoogleProvider(
        HttpClient httpClient,
        IOptions<GoogleOptions> options,
        ILogger<GoogleProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "Google";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is GoogleBase;

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async Task<Result<TResponse>> GenerateAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);

        _logger.LogDebug("Google request: {Payload}", jsonPayload);

        var endpoint = $"/v1beta/models/{llm.Name}:generateContent?key={_options.ApiKey}";

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Google API failed: {response.StatusCode}: {responseJson}");
        }

        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseJson, JsonOptions)!;

        var textContent = geminiResponse.Candidates?.FirstOrDefault()?
            .Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Google response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0;
        var outputTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0;
        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        });

        return new Result<TResponse>
        {
            Value = result,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = prompt.GetType().Name.Replace("Prompt", ""),
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    ReasoningTokens = geminiResponse.UsageMetadata?.ThoughtsTokenCount ?? 0,
                    InputCost = inputCost,
                    OutputCost = outputCost
                }
            }
        };
    }

    /// <inheritdoc />
    public Task<Result<File>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<File> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Google Gemini image generation not yet implemented");
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = new
        {
            model = $"models/{llm.Name}",
            content = new { parts = new[] { new { text = input } } }
        };

        var endpoint = $"/v1beta/models/{llm.Name}:embedContent?key={_options.ApiKey}";
        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        stopwatch.Stop();
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson, JsonOptions);

        return new Result<float[]>
        {
            Value = embeddingResponse?.Embedding?.Values ?? [],
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "Embedding",
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage()
            }
        };
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async IAsyncEnumerable<string> StreamAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);

        var endpoint = $"/v1beta/models/{llm.Name}:streamGenerateContent?alt=sse&key={_options.ApiKey}";

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];
            var chunk = JsonSerializer.Deserialize<GeminiResponse>(data, JsonOptions);

            var text = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (text != null)
                yield return text;
        }
    }

    /// <inheritdoc />
    public Task<Result<string>> TranscribeAsync(
        IAudioLlm llm,
        File audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Google audio transcription not yet implemented");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://generativelanguage.googleapis.com";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    [RequiresUnreferencedCode("JSON serialization and schema generation might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and schema generation might require types that cannot be statically analyzed and might need runtime code generation.")]
    private Dictionary<string, object> BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var parts = new List<object> { new { text = prompt.Text } };

        if (prompt.Files != null)
        {
            // Images support - use GetActualMimeType() to detect real MIME type from binary data
            foreach (var file in prompt.Files.Where(f => f.IsImage))
            {
                parts.Insert(0, new
                {
                    inlineData = new
                    {
                        mimeType = file.GetActualMimeType(),
                        data = file.ToBase64()
                    }
                });
            }

            // PDF document support (Gemini supports PDFs natively)
            foreach (var file in prompt.Files.Where(f => f.IsDocument && f.MimeType == "application/pdf"))
            {
                parts.Insert(0, new
                {
                    inlineData = new
                    {
                        mimeType = file.MimeType,
                        data = file.ToBase64()
                    }
                });
            }
        }

        var request = new Dictionary<string, object>
        {
            ["contents"] = new[] { new { parts } }
        };

        // Generation config
        var config = new Dictionary<string, object>
        {
            ["maxOutputTokens"] = llm.MaxTokens
        };

        // Only send temperature/topP if not default - Google accepts both but cleaner to send only non-defaults
        if (llm is GoogleBase googleLlm)
        {
            if (googleLlm.Temperature < 1.0)
                config["temperature"] = googleLlm.Temperature;
            if (googleLlm.TopP < 1.0)
                config["topP"] = googleLlm.TopP;
        }

        // Structured output
        if (responseType != typeof(string))
        {
            config["responseMimeType"] = "application/json";
            config["responseSchema"] = JsonSchemaGenerator.Generate(responseType);
        }

        request["generationConfig"] = config;

        // System instruction
        if (!string.IsNullOrEmpty(prompt.System))
        {
            request["systemInstruction"] = new { parts = new[] { new { text = prompt.System } } };
        }

        return request;
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    private static TResponse ParseResponse<TResponse>(string json)
    {
        if (typeof(TResponse) == typeof(string))
            return (TResponse)(object)json;

        var jsonContent = ExtractJson(json);

        return JsonSerializer.Deserialize<TResponse>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new CaseInsensitiveEnumConverterFactory(), new DateTimeConverterFactory() }
        }) ?? throw new JsonException("Deserialization returned null");
    }

    /// <summary>
    /// Extracts JSON content from a response that may contain markdown or other text.
    /// </summary>
    private static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var content = text.Trim();

        // If it already starts with { or [, it's likely valid JSON
        if (content.StartsWith('{') || content.StartsWith('['))
            return content;

        // Try to extract from ```json ... ``` blocks
        if (content.Contains("```json"))
        {
            var start = content.IndexOf("```json", StringComparison.Ordinal) + 7;
            var end = content.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return content[start..end].Trim();
        }

        // Try to extract from ``` ... ``` blocks
        if (content.Contains("```"))
        {
            var start = content.IndexOf("```", StringComparison.Ordinal) + 3;
            var newlinePos = content.IndexOf('\n', start);
            if (newlinePos > start)
                start = newlinePos + 1;
            var end = content.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return content[start..end].Trim();
        }

        // Try to find JSON object by locating first { and last }
        var firstBrace = content.IndexOf('{');
        var lastBrace = content.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return content[firstBrace..(lastBrace + 1)];

        // Try to find JSON array by locating first [ and last ]
        var firstBracket = content.IndexOf('[');
        var lastBracket = content.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
            return content[firstBracket..(lastBracket + 1)];

        return content;
    }
}

// Response models
internal sealed class GeminiResponse
{
    public GeminiCandidate[]? Candidates { get; set; }
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

internal sealed class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
}

internal sealed class GeminiContent
{
    public GeminiPart[]? Parts { get; set; }
}

internal sealed class GeminiPart
{
    public string? Text { get; set; }
}

internal sealed class GeminiUsageMetadata
{
    public int PromptTokenCount { get; set; }
    public int CandidatesTokenCount { get; set; }
    public int ThoughtsTokenCount { get; set; }
}

internal sealed class EmbeddingResponse
{
    public EmbeddingData? Embedding { get; set; }
}

internal sealed class EmbeddingData
{
    public float[]? Values { get; set; }
}

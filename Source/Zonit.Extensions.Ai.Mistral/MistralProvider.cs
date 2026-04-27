using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Converters;

namespace Zonit.Extensions.Ai.Mistral;

/// <summary>
/// Mistral provider implementation.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
[AiProvider("mistral")]
public sealed class MistralProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MistralProvider> _logger;
    private readonly MistralOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            MistralJsonContext.Default,
            AiJsonTypeInfoResolver.Instance,
            new DefaultJsonTypeInfoResolver())
    };

    public MistralProvider(
        HttpClient httpClient,
        IOptions<MistralOptions> options,
        ILogger<MistralProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "Mistral";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is MistralBase;

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, MistralJsonContext.Default.MistralChatRequest);

        _logger.LogDebug("Mistral request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/chat/completions", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Mistral error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Mistral API failed: {response.StatusCode}: {responseJson}");
        }

        var mistralResponse = JsonSerializer.Deserialize(responseJson, MistralJsonContext.Default.MistralResponse)!;

        var textContent = mistralResponse.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Mistral response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = mistralResponse.Usage?.PromptTokens ?? 0;
        var outputTokens = mistralResponse.Usage?.CompletionTokens ?? 0;
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
                RequestId = mistralResponse.Id,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    InputCost = inputCost,
                    OutputCost = outputCost
                }
            }
        };
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Mistral does not support image generation");
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Mistral does not support video generation");
    }

    /// <inheritdoc />
    public async Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = new MistralEmbedRequest
        {
            Model = llm.Name,
            Input = new[] { input }
        };

        var jsonPayload = JsonSerializer.Serialize(request, MistralJsonContext.Default.MistralEmbedRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/embeddings", content, cancellationToken);

        stopwatch.Stop();
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var embeddingResponse = JsonSerializer.Deserialize(responseJson, MistralJsonContext.Default.EmbeddingResponse);

        var inputTokens = embeddingResponse?.Usage?.PromptTokens ?? 0;
        var (inputCost, _) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens
        });

        return new Result<float[]>
        {
            Value = embeddingResponse?.Data?.FirstOrDefault()?.Embedding ?? [],
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "Embedding",
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    InputCost = inputCost
                }
            }
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, MistralJsonContext.Default.MistralChatRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions") { Content = content };
        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            var chunk = JsonSerializer.Deserialize(data, MistralJsonContext.Default.StreamChunk);
            var text = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;

            if (text != null)
                yield return text;
        }
    }

    /// <inheritdoc />
    public Task<Result<string>> TranscribeAsync(
        IAudioLlm llm,
        Asset audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Mistral does not support audio transcription");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.mistral.ai";
        _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private static MistralChatRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        Type responseType)
    {
        var messages = new List<MistralRequestMessage>();

        if (!string.IsNullOrEmpty(prompt.System))
            messages.Add(new MistralRequestMessage { Role = "system", Content = prompt.System });

        messages.Add(new MistralRequestMessage { Role = "user", Content = prompt.Text });

        var request = new MistralChatRequest
        {
            Model = llm.Name,
            Messages = messages,
            MaxTokens = llm.MaxTokens
        };

        if (llm is MistralBase mistralLlm)
        {
            if (mistralLlm.Temperature < 1.0)
                request.Temperature = mistralLlm.Temperature;
            if (mistralLlm.TopP < 1.0)
                request.TopP = mistralLlm.TopP;
        }

        if (responseType != typeof(string))
            request.ResponseFormat = new MistralResponseFormat { Type = "json_object" };

        return request;
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    private static TResponse ParseResponse<TResponse>(string json)
    {
        if (typeof(TResponse) == typeof(string))
            return (TResponse)(object)json;

        var jsonContent = ExtractJson(json);

        return JsonSerializer.Deserialize<TResponse>(jsonContent, JsonResponseParser.ProviderResponseOptions)
            ?? throw new JsonException("Deserialization returned null");
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
internal sealed class MistralResponse
{
    public string? Id { get; set; }
    public MistralChoice[]? Choices { get; set; }
    public MistralUsage? Usage { get; set; }
}

internal sealed class MistralChoice
{
    public MistralMessage? Message { get; set; }
}

internal sealed class MistralMessage
{
    public string? Content { get; set; }
}

internal sealed class MistralUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}

internal sealed class EmbeddingResponse
{
    public EmbeddingData[]? Data { get; set; }
    public MistralUsage? Usage { get; set; }
}

internal sealed class EmbeddingData
{
    public float[]? Embedding { get; set; }
}

internal sealed class StreamChunk
{
    public StreamChoice[]? Choices { get; set; }
}

internal sealed class StreamChoice
{
    public StreamDelta? Delta { get; set; }
}

internal sealed class StreamDelta
{
    public string? Content { get; set; }
}

// Request models (AOT-safe DTO).
internal sealed class MistralChatRequest
{
    public string Model { get; set; } = "";
    public List<MistralRequestMessage> Messages { get; set; } = new();
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public bool? Stream { get; set; }
    public MistralResponseFormat? ResponseFormat { get; set; }
}

internal sealed class MistralRequestMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal sealed class MistralResponseFormat
{
    public string Type { get; set; } = "";
}

internal sealed class MistralEmbedRequest
{
    public string Model { get; set; } = "";
    public string[] Input { get; set; } = [];
}

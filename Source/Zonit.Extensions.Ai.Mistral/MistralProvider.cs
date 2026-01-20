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

namespace Zonit.Extensions.Ai.Mistral;

/// <summary>
/// Mistral provider implementation.
/// </summary>
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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

        var mistralResponse = JsonSerializer.Deserialize<MistralResponse>(responseJson, JsonOptions)!;

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
    public Task<Result<File>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<File> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Mistral does not support image generation");
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
            model = llm.Name,
            input = new[] { input }
        };

        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/embeddings", content, cancellationToken);

        stopwatch.Stop();
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson, JsonOptions);

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
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async IAsyncEnumerable<string> StreamAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        request["stream"] = true;

        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);

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

            var chunk = JsonSerializer.Deserialize<StreamChunk>(data, JsonOptions);
            var text = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;

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

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    private Dictionary<string, object> BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        Type responseType)
    {
        var messages = new List<object>();

        if (!string.IsNullOrEmpty(prompt.System))
            messages.Add(new { role = "system", content = prompt.System });

        messages.Add(new { role = "user", content = prompt.Text });

        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["messages"] = messages,
            ["max_tokens"] = llm.MaxTokens
        };

        if (llm is MistralBase mistralLlm)
        {
            request["temperature"] = mistralLlm.Temperature;
            request["top_p"] = mistralLlm.TopP;
        }

        // Structured output
        if (responseType != typeof(string))
        {
            request["response_format"] = new { type = "json_object" };
        }

        return request;
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    private static TResponse ParseResponse<TResponse>(string json)
    {
        if (typeof(TResponse) == typeof(string))
            return (TResponse)(object)json;

        // Try to extract JSON from markdown code blocks
        var jsonContent = json;
        if (json.Contains("```json"))
        {
            var start = json.IndexOf("```json", StringComparison.Ordinal) + 7;
            var end = json.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                jsonContent = json[start..end].Trim();
        }
        else if (json.Contains("```"))
        {
            var start = json.IndexOf("```", StringComparison.Ordinal) + 3;
            var end = json.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                jsonContent = json[start..end].Trim();
        }

        return JsonSerializer.Deserialize<TResponse>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() }
        }) ?? throw new JsonException("Deserialization returned null");
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

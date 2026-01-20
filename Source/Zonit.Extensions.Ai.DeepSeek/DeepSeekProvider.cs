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

namespace Zonit.Extensions.Ai.DeepSeek;

/// <summary>
/// DeepSeek provider implementation.
/// Uses OpenAI-compatible API.
/// </summary>
[AiProvider("deepseek")]
public sealed class DeepSeekProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekProvider> _logger;
    private readonly DeepSeekOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeepSeekProvider(
        HttpClient httpClient,
        IOptions<DeepSeekOptions> options,
        ILogger<DeepSeekProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "DeepSeek";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is DeepSeekBase;

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

        _logger.LogDebug("DeepSeek request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/chat/completions", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("DeepSeek error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"DeepSeek API failed: {response.StatusCode}: {responseJson}");
        }

        var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson, JsonOptions)!;

        var textContent = deepSeekResponse.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in DeepSeek response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = deepSeekResponse.Usage?.PromptTokens ?? 0;
        var outputTokens = deepSeekResponse.Usage?.CompletionTokens ?? 0;
        var cachedTokens = deepSeekResponse.Usage?.PromptCacheHitTokens ?? 0;
        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cachedTokens
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
                RequestId = deepSeekResponse.Id,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    CachedTokens = cachedTokens,
                    ReasoningTokens = deepSeekResponse.Usage?.ReasoningTokens ?? 0,
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
        throw new NotSupportedException("DeepSeek does not support image generation");
    }

    /// <inheritdoc />
    public Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("DeepSeek does not support embeddings");
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
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/chat/completions") { Content = content };
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
        throw new NotSupportedException("DeepSeek does not support audio transcription");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.deepseek.com";
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

        if (llm is DeepSeekBase deepSeekLlm)
        {
            request["temperature"] = deepSeekLlm.Temperature;
            request["top_p"] = deepSeekLlm.TopP;
        }

        // Structured output
        if (responseType != typeof(string))
        {
            request["response_format"] = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "response",
                    schema = JsonSchemaGenerator.Generate(responseType),
                    strict = true
                }
            };
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
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        }) ?? throw new JsonException("Deserialization returned null");
    }
}

// Response models
internal sealed class DeepSeekResponse
{
    public string? Id { get; set; }
    public DeepSeekChoice[]? Choices { get; set; }
    public DeepSeekUsage? Usage { get; set; }
}

internal sealed class DeepSeekChoice
{
    public DeepSeekMessage? Message { get; set; }
}

internal sealed class DeepSeekMessage
{
    public string? Content { get; set; }
    public string? ReasoningContent { get; set; }
}

internal sealed class DeepSeekUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int PromptCacheHitTokens { get; set; }
    public int ReasoningTokens { get; set; }
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
    public string? ReasoningContent { get; set; }
}

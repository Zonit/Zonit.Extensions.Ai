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
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, DeepSeekJsonContext.Default.DeepSeekChatRequest);

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

        var deepSeekResponse = JsonSerializer.Deserialize(responseJson, DeepSeekJsonContext.Default.DeepSeekResponse)!;

        var textContent = deepSeekResponse.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrEmpty(textContent))
            throw new AiEmptyResponseException(AiResponseError.EmptyAfterRetries, "DeepSeek returned no text — server-side data loss; usually transient, re-run the operation.");

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
                PromptName = PromptNameResolver.Resolve(prompt),
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
    public Task<Result<Asset>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("DeepSeek does not support image generation");
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("DeepSeek does not support video generation");
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
    public async IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, DeepSeekJsonContext.Default.DeepSeekChatRequest);

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

            var chunk = JsonSerializer.Deserialize(data, DeepSeekJsonContext.Default.StreamChunk);
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

    private static DeepSeekChatRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var messages = new List<DeepSeekRequestMessage>();


        messages.Add(new DeepSeekRequestMessage { Role = "user", Content = prompt.Text });

        var request = new DeepSeekChatRequest
        {
            Model = llm.Name,
            Messages = messages,
            MaxTokens = llm.MaxTokens
        };

        if (llm is DeepSeekBase deepSeekLlm)
        {
            if (deepSeekLlm.Temperature < 1.0)
                request.Temperature = deepSeekLlm.Temperature;
            if (deepSeekLlm.TopP < 1.0)
                request.TopP = deepSeekLlm.TopP;
        }

        if (responseType != typeof(string))
        {
            request.ResponseFormat = new DeepSeekResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new DeepSeekJsonSchemaSpec
                {
                    Name = "response",
                    Schema = AiSchemaRegistry.GetSchema(responseType),
                    Strict = true
                }
            };
        }

        return request;
    }

    private static TResponse ParseResponse<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(string json)
        => JsonResponseParser.DeserializeStructured<TResponse>(json);
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

// Request models (AOT-safe DTO).
internal sealed class DeepSeekChatRequest
{
    public string Model { get; set; } = "";
    public List<DeepSeekRequestMessage> Messages { get; set; } = new();
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public bool? Stream { get; set; }
    public DeepSeekResponseFormat? ResponseFormat { get; set; }
}

internal sealed class DeepSeekRequestMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal sealed class DeepSeekResponseFormat
{
    public string Type { get; set; } = "";
    public DeepSeekJsonSchemaSpec? JsonSchema { get; set; }
}

internal sealed class DeepSeekJsonSchemaSpec
{
    public string Name { get; set; } = "";
    public JsonElement Schema { get; set; }
    public bool Strict { get; set; }
}

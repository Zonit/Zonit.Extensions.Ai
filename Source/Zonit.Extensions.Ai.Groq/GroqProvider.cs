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

namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Groq provider implementation.
/// Uses OpenAI-compatible API with ultra-fast LPU inference.
/// </summary>
[AiProvider("groq")]
public sealed class GroqProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqProvider> _logger;
    private readonly GroqOptions _options;

    public GroqProvider(
        HttpClient httpClient,
        IOptions<GroqOptions> options,
        ILogger<GroqProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "Groq";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is GroqBase;

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, GroqJsonContext.Default.GroqChatRequest);

        _logger.LogDebug("Groq request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/openai/v1/chat/completions", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Groq error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Groq API failed: {response.StatusCode}: {responseJson}");
        }

        var groqResponse = JsonSerializer.Deserialize(responseJson, GroqJsonContext.Default.GroqResponse)!;

        var textContent = groqResponse.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Groq response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = groqResponse.Usage?.PromptTokens ?? 0;
        var outputTokens = groqResponse.Usage?.CompletionTokens ?? 0;
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
                PromptName = PromptNameResolver.Resolve(prompt),
                Duration = stopwatch.Elapsed,
                RequestId = groqResponse.Id,
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
        throw new NotSupportedException("Groq does not support image generation");
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Groq does not support video generation");
    }

    /// <inheritdoc />
    public Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Groq does not support embeddings");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, GroqJsonContext.Default.GroqChatRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/openai/v1/chat/completions") { Content = content };
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

            var chunk = JsonSerializer.Deserialize(data, GroqJsonContext.Default.GroqStreamChunk);
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
        throw new NotSupportedException("Groq does not support audio transcription via this interface");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.groq.com";
        _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private static GroqChatRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var messages = new List<GroqRequestMessage>();


        messages.Add(new GroqRequestMessage { Role = "user", Content = prompt.Text });

        var request = new GroqChatRequest
        {
            Model = llm.Name,
            Messages = messages,
            MaxTokens = llm.MaxTokens
        };

        if (llm is GroqBase groqLlm)
        {
            if (groqLlm.Temperature < 1.0)
                request.Temperature = groqLlm.Temperature;
            if (groqLlm.TopP < 1.0)
                request.TopP = groqLlm.TopP;
        }

        if (responseType != typeof(string))
        {
            request.ResponseFormat = new GroqResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new GroqJsonSchemaSpec
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
internal sealed class GroqResponse
{
    public string? Id { get; set; }
    public GroqChoice[]? Choices { get; set; }
    public GroqUsage? Usage { get; set; }
}

internal sealed class GroqChoice
{
    public GroqMessage? Message { get; set; }
}

internal sealed class GroqMessage
{
    public string? Content { get; set; }
}

internal sealed class GroqUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}

internal sealed class GroqStreamChunk
{
    public GroqStreamChoice[]? Choices { get; set; }
}

internal sealed class GroqStreamChoice
{
    public GroqStreamDelta? Delta { get; set; }
}

internal sealed class GroqStreamDelta
{
    public string? Content { get; set; }
}

// Request models (AOT-safe DTO).
internal sealed class GroqChatRequest
{
    public string Model { get; set; } = "";
    public List<GroqRequestMessage> Messages { get; set; } = new();
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public bool? Stream { get; set; }
    public GroqResponseFormat? ResponseFormat { get; set; }
}

internal sealed class GroqRequestMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal sealed class GroqResponseFormat
{
    public string Type { get; set; } = "";
    public GroqJsonSchemaSpec? JsonSchema { get; set; }
}

internal sealed class GroqJsonSchemaSpec
{
    public string Name { get; set; } = "";
    public JsonElement Schema { get; set; }
    public bool Strict { get; set; }
}

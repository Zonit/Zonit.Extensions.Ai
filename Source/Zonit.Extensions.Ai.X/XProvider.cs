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

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// X (Grok) provider implementation.
/// Uses OpenAI-compatible API.
/// </summary>
[AiProvider("x")]
public sealed class XProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<XProvider> _logger;
    private readonly AiOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public XProvider(
        HttpClient httpClient, 
        IOptions<AiOptions> options, 
        ILogger<XProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        
        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "X";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is XBase;

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async Task<AiResult<TResponse>> GenerateAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);
        
        _logger.LogDebug("X request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/chat/completions", content, cancellationToken);
        
        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("X error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"X API failed: {response.StatusCode}: {responseJson}");
        }

        var xResponse = JsonSerializer.Deserialize<XResponse>(responseJson, JsonOptions)!;
        
        var textContent = xResponse.Choices?.FirstOrDefault()?.Message?.Content;
        
        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in X response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = xResponse.Usage?.PromptTokens ?? 0;
        var outputTokens = xResponse.Usage?.CompletionTokens ?? 0;
        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        });

        return new AiResult<TResponse>
        {
            Value = result,
            Model = llm.Name,
            Provider = Name,
            PromptName = prompt.GetType().Name.Replace("Prompt", ""),
            Duration = stopwatch.Elapsed,
            RequestId = xResponse.Id,
            Usage = new TokenUsage
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                ReasoningTokens = xResponse.Usage?.CompletionTokensDetails?.ReasoningTokens ?? 0,
                InputCost = inputCost,
                OutputCost = outputCost
            }
        };
    }

    /// <inheritdoc />
    public Task<AiResult<AiFile>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<AiFile> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("X does not support image generation");
    }

    /// <inheritdoc />
    public Task<AiResult<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("X does not support embeddings");
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
    public Task<AiResult<string>> TranscribeAsync(
        IAudioLlm llm,
        AiFile audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("X does not support audio transcription");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.X.BaseUrl ?? "https://api.x.ai";
        _httpClient.BaseAddress = new Uri(baseUrl);
        
        if (!string.IsNullOrEmpty(_options.X.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.X.ApiKey);
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

        var content = new List<object> { new { type = "text", text = prompt.Text } };

        if (prompt.Files != null)
        {
            foreach (var file in prompt.Files.Where(f => f.IsImage))
            {
                content.Add(new
                {
                    type = "image_url",
                    image_url = new { url = file.ToDataUrl() }
                });
            }
        }

        messages.Add(new { role = "user", content });

        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["messages"] = messages,
            ["max_tokens"] = llm.MaxTokens
        };

        if (llm is XChatBase xLlm)
        {
            request["temperature"] = xLlm.Temperature;
            request["top_p"] = xLlm.TopP;
        }

        // Reasoning
        if (llm is XReasoningBase { Reason: not null } reasoningLlm)
        {
            request["reasoning_effort"] = reasoningLlm.Reason.Value.ToString().ToLowerInvariant();
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

        return JsonSerializer.Deserialize<TResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        }) ?? throw new JsonException("Deserialization returned null");
    }
}

// Response models
internal sealed class XResponse
{
    public string? Id { get; set; }
    public XChoice[]? Choices { get; set; }
    public XUsage? Usage { get; set; }
}

internal sealed class XChoice
{
    public XMessage? Message { get; set; }
}

internal sealed class XMessage
{
    public string? Content { get; set; }
}

internal sealed class XUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public XTokenDetails? CompletionTokensDetails { get; set; }
}

internal sealed class XTokenDetails
{
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
}

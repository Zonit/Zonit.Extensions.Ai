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

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Anthropic Claude provider implementation.
/// </summary>
[AiProvider("anthropic")]
public sealed class AnthropicProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicProvider> _logger;
    private readonly AiOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AnthropicProvider(
        HttpClient httpClient, 
        IOptions<AiOptions> options, 
        ILogger<AnthropicProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        
        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "Anthropic";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is AnthropicBase;

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
        
        _logger.LogDebug("Anthropic request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);
        
        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Anthropic API failed: {response.StatusCode}: {responseJson}");
        }

        var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(responseJson, JsonOptions)!;
        
        var textContent = anthropicResponse.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
        
        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Anthropic response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = anthropicResponse.Usage?.InputTokens ?? 0;
        var outputTokens = anthropicResponse.Usage?.OutputTokens ?? 0;
        var cachedTokens = anthropicResponse.Usage?.CacheReadInputTokens ?? 0;
        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cachedTokens
        });

        return new AiResult<TResponse>
        {
            Value = result,
            Model = llm.Name,
            Provider = Name,
            PromptName = prompt.GetType().Name.Replace("Prompt", ""),
            Duration = stopwatch.Elapsed,
            RequestId = anthropicResponse.Id,
            Usage = new TokenUsage
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CachedTokens = cachedTokens,
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
        throw new NotSupportedException("Anthropic does not support image generation");
    }

    /// <inheritdoc />
    public Task<AiResult<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Anthropic does not support embeddings");
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
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/messages") { Content = content };
        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];
            var chunk = JsonSerializer.Deserialize<StreamEvent>(data, JsonOptions);
            
            if (chunk?.Type == "content_block_delta" && chunk.Delta?.Text != null)
                yield return chunk.Delta.Text;
        }
    }

    /// <inheritdoc />
    public Task<AiResult<string>> TranscribeAsync(
        IAudioLlm llm,
        AiFile audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Anthropic does not support audio transcription");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.Anthropic.BaseUrl ?? "https://api.anthropic.com";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        
        if (!string.IsNullOrEmpty(_options.Anthropic.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.Anthropic.ApiKey);
        }
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    private Dictionary<string, object> BuildRequest<TResponse>(
        ILlm llm, 
        IPrompt<TResponse> prompt, 
        Type responseType)
    {
        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["max_tokens"] = llm.MaxTokens
        };

        if (!string.IsNullOrEmpty(prompt.System))
            request["system"] = prompt.System;

        var content = new List<object> { new { type = "text", text = prompt.Text } };

        if (prompt.Files != null)
        {
            foreach (var file in prompt.Files.Where(f => f.IsImage))
            {
                content.Insert(0, new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = file.MimeType,
                        data = file.ToBase64()
                    }
                });
            }
        }

        request["messages"] = new[] { new { role = "user", content } };

        // Model-specific settings
        if (llm is AnthropicBase anthropicLlm)
        {
            request["temperature"] = anthropicLlm.Temperature;
            request["top_p"] = anthropicLlm.TopP;
        }

        // Extended thinking
        if (llm is AnthropicBase thinkingLlm && thinkingLlm.ThinkingBudget.HasValue)
        {
            request["thinking"] = new
            {
                type = "enabled",
                budget_tokens = thinkingLlm.ThinkingBudget.Value
            };
        }

        // Tools from LLM
        if (llm.Tools != null && llm.Tools.Length > 0)
        {
            request["tools"] = llm.Tools.OfType<FunctionTool>().Select(f => new
            {
                name = f.Name,
                description = f.Description,
                input_schema = f.Parameters
            }).ToList();
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
internal sealed class AnthropicResponse
{
    public string? Id { get; set; }
    public AnthropicContent[]? Content { get; set; }
    public AnthropicUsage? Usage { get; set; }
}

internal sealed class AnthropicContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

internal sealed class AnthropicUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadInputTokens { get; set; }
    public int CacheCreationInputTokens { get; set; }
}

internal sealed class StreamEvent
{
    public string? Type { get; set; }
    public StreamDelta? Delta { get; set; }
}

internal sealed class StreamDelta
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

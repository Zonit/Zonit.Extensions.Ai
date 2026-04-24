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
using Zonit.Extensions;
using Zonit.Extensions.Ai.Converters;

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// X (Grok) provider implementation.
/// Uses Responses API (/v1/responses) with Agent Tools for web/X search.
/// </summary>
[AiProvider("x")]
public sealed class XProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<XProvider> _logger;
    private readonly XOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public XProvider(
        HttpClient httpClient,
        IOptions<XOptions> options,
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
    public async Task<Result<TResponse>> GenerateAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);

        _logger.LogDebug("X request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/responses", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("X error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"X API failed: {response.StatusCode}: {responseJson}");
        }

        var xResponse = JsonSerializer.Deserialize<XResponse>(responseJson, JsonOptions)!;

        // Responses API uses 'output' array instead of 'choices'
        var textContent = xResponse.Output?.FirstOrDefault(o => o.Type == "message")?.Content
            ?.FirstOrDefault(c => c.Type == "output_text")?.Text;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in X response");

        var result = ParseResponse<TResponse>(textContent);

        // Responses API (POST /v1/responses) returns input_tokens/output_tokens.
        // Chat Completions API (and GET /v1/responses/{id}) returns prompt_tokens/completion_tokens.
        // We read both to stay compatible with either shape.
        var inputTokens = xResponse.Usage?.InputTokens ?? xResponse.Usage?.PromptTokens ?? 0;
        var outputTokens = xResponse.Usage?.OutputTokens ?? xResponse.Usage?.CompletionTokens ?? 0;
        var cachedTokens = xResponse.Usage?.InputTokensDetails?.CachedTokens
            ?? xResponse.Usage?.PromptTokensDetails?.CachedTokens
            ?? 0;
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
                RequestId = xResponse.Id,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    CachedTokens = cachedTokens,
                    ReasoningTokens = xResponse.Usage?.OutputTokensDetails?.ReasoningTokens
                        ?? xResponse.Usage?.CompletionTokensDetails?.ReasoningTokens
                        ?? 0,
                    InputCost = inputCost,
                    OutputCost = outputCost
                }
            }
        };
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async Task<Result<Asset>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["prompt"] = prompt.Text,
            ["n"] = 1,
            ["response_format"] = "b64_json"
        };

        // X.ai supports aspect_ratio but NOT quality parameter
        if (!string.IsNullOrEmpty(llm.AspectRatioValue))
            request["aspect_ratio"] = llm.AspectRatioValue;

        // If source image is provided, use it for image editing
        // X.ai expects image_url in data URL format: data:image/jpeg;base64,...
        var sourceImage = prompt.Files?.FirstOrDefault(f => f.IsImage);
        if (sourceImage is { HasValue: true } img)
            request["image_url"] = img.DataUrl;

        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);
        _logger.LogDebug("X image generation request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/images/generations", content, cancellationToken);
        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("X image generation error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"X Image API failed: {response.StatusCode}: {responseJson}");
        }

        var imageResponse = JsonSerializer.Deserialize<XImageResponse>(responseJson, JsonOptions);

        if (imageResponse?.Data == null || imageResponse.Data.Length == 0)
            throw new InvalidOperationException("No image data in response");

        var imageBytes = Convert.FromBase64String(imageResponse.Data[0].B64Json ?? "");
        Asset generatedImage = new(imageBytes, "generated.png");

        var imageCost = llm.GetImageGenerationPrice();

        return new Result<Asset>
        {
            Value = generatedImage,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = prompt.GetType().Name.Replace("Prompt", ""),
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage
                {
                    OutputCost = imageCost
                }
            }
        };
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Check for source image or video in prompt.Files
        var sourceImage = prompt.Files?.FirstOrDefault(f => f.IsImage);
        var sourceVideo = prompt.Files?.FirstOrDefault(f => f.IsVideo);

        // Step 1: Create video generation/edit task
        var createRequest = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["prompt"] = prompt.Text,
            ["duration"] = llm.DurationSeconds,
            ["resolution"] = llm.QualityValue,
            ["aspect_ratio"] = llm.AspectRatioValue
        };

        // Video from Image: adds image parameter
        // X.ai expects: {"image": {"url": "<data url or public url>"}}
        if (sourceImage is { HasValue: true } img)
            createRequest["image"] = new Dictionary<string, string> { ["url"] = img.DataUrl };

        // Video Edit: adds video parameter (requires public URL, not base64)
        // X.ai expects: {"video": {"url": "<public url>"}}
        // Note: For video editing, use a public URL - base64 is not supported for video input
        if (sourceVideo is { HasValue: true } vid)
            createRequest["video"] = new Dictionary<string, string> { ["url"] = vid.DataUrl };

        // Choose endpoint: /v1/videos/edits for video editing, /v1/videos/generations for all else
        var endpoint = sourceVideo is { HasValue: true } ? "/v1/videos/edits" : "/v1/videos/generations";

        var jsonPayload = JsonSerializer.Serialize(createRequest, JsonOptions);
        _logger.LogDebug("X video generation request: {Payload}", jsonPayload);

        using var createContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var createResponse = await _httpClient.PostAsync(endpoint, createContent, cancellationToken);

        var createResponseJson = await createResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!createResponse.IsSuccessStatusCode)
        {
            _logger.LogError("X video generation error: {Status} - {Response}", createResponse.StatusCode, createResponseJson);
            throw new HttpRequestException($"X Video API failed: {createResponse.StatusCode}: {createResponseJson}");
        }

        _logger.LogDebug("X video generation response: {Response}", createResponseJson);

        var videoTask = JsonSerializer.Deserialize<XVideoTaskResponse>(createResponseJson, JsonOptions);

        // API may return 'id' or 'request_id'
        var taskId = videoTask?.Id ?? videoTask?.RequestId;

        if (string.IsNullOrEmpty(taskId))
            throw new InvalidOperationException($"No task ID in video generation response: {createResponseJson}");

        // Step 2: Poll for completion
        var maxWaitTime = TimeSpan.FromMinutes(10);
        var pollInterval = TimeSpan.FromSeconds(3);
        var elapsed = TimeSpan.Zero;

        XVideoStatusResponse? statusResponse = null;
        while (elapsed < maxWaitTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(pollInterval, cancellationToken);
            elapsed += pollInterval;

            // X.ai video status endpoint: GET /v1/videos/{request_id}
            using var statusResp = await _httpClient.GetAsync($"/v1/videos/{taskId}", cancellationToken);
            var statusJson = await statusResp.Content.ReadAsStringAsync(cancellationToken);

            if (!statusResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("X video status check failed: {Status} - {Response}", statusResp.StatusCode, statusJson);
                continue;
            }

            statusResponse = JsonSerializer.Deserialize<XVideoStatusResponse>(statusJson, JsonOptions);

            // Check status (API may return 'status' or 'state' field)
            var currentStatus = statusResponse?.State ?? statusResponse?.Status;

            _logger.LogDebug("Video status response: status={Status}, hasVideoUrl={HasVideo}",
                currentStatus, statusResponse?.Video?.Url != null);

            // X.ai API returns video.url when generation is complete (no status field in final response)
            if (!string.IsNullOrEmpty(statusResponse?.Video?.Url))
                break;

            if (currentStatus == "completed" || currentStatus == "succeeded")
                break;

            if (currentStatus == "failed" || currentStatus == "error")
                throw new InvalidOperationException($"Video generation failed: {statusResponse?.Error ?? "Unknown error"}");

            _logger.LogDebug("Video generation in progress, elapsed: {Elapsed}s", elapsed.TotalSeconds);
        }

        stopwatch.Stop();

        // Step 3: Download the video (X.ai returns 'video.url' in completed response)
        var videoUrl = statusResponse?.Video?.Url ?? statusResponse?.Url ?? statusResponse?.Output?.Url ?? statusResponse?.VideoUrl;
        if (string.IsNullOrEmpty(videoUrl))
            throw new InvalidOperationException("No video URL in response");

        using var videoResponse = await _httpClient.GetAsync(videoUrl, cancellationToken);
        videoResponse.EnsureSuccessStatusCode();

        var videoBytes = await videoResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        Asset generatedVideo = new(videoBytes, "generated.mp4");

        var videoCost = llm.GetVideoGenerationPrice();

        return new Result<Asset>
        {
            Value = generatedVideo,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = prompt.GetType().Name.Replace("Prompt", ""),
                Duration = stopwatch.Elapsed,
                RequestId = taskId,
                Usage = new TokenUsage
                {
                    OutputCost = videoCost
                }
            }
        };
    }

    /// <inheritdoc />
    public Task<Result<float[]>> EmbedAsync(
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
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/responses") { Content = content };
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
            var text = chunk?.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text;

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
        throw new NotSupportedException("X does not support audio transcription");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.x.ai";
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
        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["max_output_tokens"] = llm.MaxTokens
        };

        // Responses API uses 'instructions' for system message
        if (!string.IsNullOrEmpty(prompt.System))
            request["instructions"] = prompt.System;

        // Build input - Responses API format
        var input = new List<object>();
        var content = new List<object> { new { type = "input_text", text = prompt.Text } };

        if (prompt.Files != null)
        {
            foreach (var file in prompt.Files)
            {
                if (file.IsImage)
                {
                    content.Add(new
                    {
                        type = "input_image",
                        image_url = file.DataUrl
                    });
                }
            }
        }

        input.Add(new { role = "user", content });
        request["input"] = input;

        // Only send temperature/top_p if not default - OpenAI-compatible API recommends altering one, not both
        if (llm is XChatBase xLlm)
        {
            if (xLlm.Temperature < 1.0)
                request["temperature"] = xLlm.Temperature;
            if (xLlm.TopP < 1.0)
                request["top_p"] = xLlm.TopP;

            // WebSearch support - using Responses API Agent Tools
            // NOTE: Responses API uses separate 'web_search' and 'x_search' tools
            if (xLlm.WebSearch != null && xLlm.WebSearch.Mode != ModeType.Never)
            {
                var tools = BuildAgentTools(xLlm.WebSearch);
                if (tools.Count > 0)
                    request["tools"] = tools;
            }
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

    /// <summary>
    /// Builds agent tools array for the Agent Tools API.
    /// Uses 'live_search' type for /v1/chat/completions endpoint compatibility.
    /// </summary>
    private static List<object> BuildAgentTools(Search webSearch)
    {
        var tools = new List<object>();

        // Responses API uses separate web_search and x_search tools
        var hasWebSource = webSearch.Sources?.Any(s => s.Type == SourceType.Web) ?? true;
        var hasXSource = webSearch.Sources?.Any(s => s.Type == SourceType.X) ?? true;

        // Build web_search tool
        if (hasWebSource)
        {
            var webSearchConfig = new Dictionary<string, object> { ["type"] = "web_search" };

            var webSource = webSearch.Sources?.OfType<WebSearchSource>().FirstOrDefault();
            if (webSource != null)
            {
                if (webSource.AllowedWebsites?.Length > 0)
                    webSearchConfig["allowed_domains"] = webSource.AllowedWebsites.Take(5).ToArray();
                if (webSource.ExcludedWebsites?.Length > 0)
                    webSearchConfig["excluded_domains"] = webSource.ExcludedWebsites.Take(5).ToArray();
            }

            tools.Add(webSearchConfig);
        }

        // Build x_search tool
        if (hasXSource)
        {
            var xSearchConfig = new Dictionary<string, object> { ["type"] = "x_search" };

            var xSource = webSearch.Sources?.OfType<XSearchSource>().FirstOrDefault();
            if (xSource != null)
            {
                if (xSource.IncludedXHandles?.Length > 0)
                    xSearchConfig["allowed_x_handles"] = xSource.IncludedXHandles.Take(10).ToArray();
                if (xSource.ExcludedXHandles?.Length > 0)
                    xSearchConfig["excluded_x_handles"] = xSource.ExcludedXHandles.Take(10).ToArray();
            }

            // Date filters (shared between web and x search)
            if (webSearch.FromDate.HasValue)
                xSearchConfig["from_date"] = webSearch.FromDate.Value.ToString("yyyy-MM-dd");
            if (webSearch.ToDate.HasValue)
                xSearchConfig["to_date"] = webSearch.ToDate.Value.ToString("yyyy-MM-dd");

            tools.Add(xSearchConfig);
        }

        return tools;
    }

}

// Response models
// Responses API format
internal sealed class XResponse
{
    public string? Id { get; set; }
    public XOutput[]? Output { get; set; }
    public XUsage? Usage { get; set; }
    public string? Status { get; set; }
}

internal sealed class XOutput
{
    public string? Type { get; set; } // "message", "reasoning"
    public XOutputContent[]? Content { get; set; }
}

internal sealed class XOutputContent
{
    public string? Type { get; set; } // "output_text", "summary_text"
    public string? Text { get; set; }
}

internal sealed class XUsage
{
    // Responses API (POST /v1/responses) shape
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public XInputTokensDetails? InputTokensDetails { get; set; }
    public XOutputTokensDetails? OutputTokensDetails { get; set; }

    // Chat Completions API / GET /v1/responses/{id} shape
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public XPromptTokensDetails? PromptTokensDetails { get; set; }
    public XTokenDetails? CompletionTokensDetails { get; set; }
}

internal sealed class XInputTokensDetails
{
    public int CachedTokens { get; set; }
}

internal sealed class XOutputTokensDetails
{
    public int ReasoningTokens { get; set; }
}

internal sealed class XPromptTokensDetails
{
    public int CachedTokens { get; set; }
}

internal sealed class XTokenDetails
{
    public int ReasoningTokens { get; set; }
}

internal sealed class StreamChunk
{
    public XOutput[]? Output { get; set; }
}

// Image generation response models
internal sealed class XImageResponse
{
    public XImageData[]? Data { get; set; }
}

internal sealed class XImageData
{
    public string? B64Json { get; set; }
    public string? Url { get; set; }
    public string? RevisedPrompt { get; set; }
}

// Video generation response models
internal sealed class XVideoTaskResponse
{
    public string? Id { get; set; }
    public string? RequestId { get; set; }
    public string? Status { get; set; }
}

internal sealed class XVideoStatusResponse
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public string? State { get; set; }
    public string? Error { get; set; }
    public string? Url { get; set; }
    public string? VideoUrl { get; set; }
    public XVideoOutput? Output { get; set; }
    /// <summary>
    /// X.ai returns video data in this nested object when generation is complete.
    /// </summary>
    public XVideoData? Video { get; set; }
}

/// <summary>
/// X.ai video response data containing URL and metadata.
/// </summary>
internal sealed class XVideoData
{
    public string? Url { get; set; }
    public int? Duration { get; set; }
}

internal sealed class XVideoOutput
{
    public string? Url { get; set; }
}

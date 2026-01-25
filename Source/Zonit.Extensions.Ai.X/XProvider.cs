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

        var inputTokens = xResponse.Usage?.PromptTokens ?? 0;
        var outputTokens = xResponse.Usage?.CompletionTokens ?? 0;
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
                RequestId = xResponse.Id,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    ReasoningTokens = xResponse.Usage?.CompletionTokensDetails?.ReasoningTokens ?? 0,
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
        throw new NotSupportedException("X does not support image generation");
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
    public XOutput[]? Output { get; set; }
}

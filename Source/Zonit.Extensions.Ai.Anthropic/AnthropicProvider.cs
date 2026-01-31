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

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Anthropic Claude provider implementation.
/// </summary>
[AiProvider("anthropic")]
public sealed class AnthropicProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicProvider> _logger;
    private readonly AnthropicOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AnthropicProvider(
        HttpClient httpClient,
        IOptions<AnthropicOptions> options,
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
    public async Task<Result<TResponse>> GenerateAsync<TResponse>(
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

        return new Result<TResponse>
        {
            Value = result,
            MetaData = new MetaData
            {
                Model = llm,
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
            }
        };
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Anthropic does not support image generation");
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Anthropic does not support video generation");
    }

    /// <inheritdoc />
    public Task<Result<float[]>> EmbedAsync(
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
    public Task<Result<string>> TranscribeAsync(
        IAudioLlm llm,
        Asset audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Anthropic does not support audio transcription");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.anthropic.com";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        }
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    private Dictionary<string, object> BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        Type responseType)
    {
        var maxTokens = llm.MaxTokens;

        // When extended thinking is enabled, ensure max_tokens > thinking.budget_tokens
        if (llm is AnthropicBase thinkingLlm && thinkingLlm.ThinkingBudget.HasValue)
        {
            var requiredMinTokens = thinkingLlm.ThinkingBudget.Value + 1024; // Add buffer for actual response
            if (maxTokens < requiredMinTokens)
                maxTokens = requiredMinTokens;
        }

        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["max_tokens"] = maxTokens
        };

        // Build system prompt - add JSON schema instruction for structured output
        var systemPrompt = prompt.System ?? "";
        string? userJsonReminder = null;
        if (responseType != typeof(string))
        {
            var schema = JsonSchemaGenerator.Generate(responseType);
            var schemaJson = schema.ToString();

            // Strong instruction at system level
            var jsonInstruction = $@"

CRITICAL JSON OUTPUT REQUIREMENTS:
1. You MUST respond with a SINGLE JSON OBJECT (starting with a curly brace)
2. Do NOT respond with a JSON array []
3. Do NOT wrap response in markdown code blocks
4. Do NOT add any explanation or text before/after the JSON
5. The JSON object MUST match this exact schema:

{schemaJson}

Remember: Your response must start with the opening curly brace and be a valid JSON object matching the schema above.
";
            systemPrompt = string.IsNullOrEmpty(systemPrompt) ? jsonInstruction.Trim() : systemPrompt + jsonInstruction;

            // Reminder to add at the end of user message
            userJsonReminder = "\n\nRespond with a JSON object matching the schema. Start your response with {";
        }

        if (!string.IsNullOrEmpty(systemPrompt))
            request["system"] = systemPrompt;

        // Build user message content with optional JSON reminder
        var userText = prompt.Text;
        if (!string.IsNullOrEmpty(userJsonReminder))
            userText += userJsonReminder;

        var content = new List<object> { new { type = "text", text = userText } };

        if (prompt.Files != null)
        {
            foreach (var file in prompt.Files.Where(f => f.IsImage))
            {
                // Use MediaType which is detected from binary signature (magic bytes)
                content.Insert(0, new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = file.MediaType.Value,
                        data = file.Base64
                    }
                });
            }

            // PDF document support (Anthropic supports PDFs via document type)
            foreach (var file in prompt.Files.Where(f => f.IsDocument))
            {
                if (file.MediaType == Asset.MimeType.ApplicationPdf)
                {
                    content.Insert(0, new
                    {
                        type = "document",
                        source = new
                        {
                            type = "base64",
                            media_type = file.MediaType.Value,
                            data = file.Base64
                        }
                    });
                }
            }
        }

        // Use prefill technique for structured output - start assistant response with "{"
        var messages = new List<object> { new { role = "user", content } };
        if (responseType != typeof(string))
        {
            messages.Add(new { role = "assistant", content = "{" });
        }
        request["messages"] = messages;

        // Model-specific settings
        // Anthropic API does not allow both temperature and top_p to be set simultaneously
        // Only send one parameter, and only if it's not the default value
        if (llm is AnthropicBase anthropicLlm)
        {
            // Prefer top_p if explicitly set (not default 1.0), otherwise use temperature if not default
            if (anthropicLlm.TopP < 1.0)
            {
                request["top_p"] = anthropicLlm.TopP;
            }
            else if (anthropicLlm.Temperature < 1.0)
            {
                request["temperature"] = anthropicLlm.Temperature;
            }
            // If both are default (1.0), don't send either - Anthropic uses its own defaults

            // Extended thinking (for models that support it)
            if (anthropicLlm.ThinkingBudget.HasValue)
            {
                request["thinking"] = new
                {
                    type = "enabled",
                    budget_tokens = anthropicLlm.ThinkingBudget.Value
                };
            }
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

        var jsonContent = ExtractJson(json);

        // Trim whitespace first
        jsonContent = jsonContent.Trim();

        // When using prefill technique, the response doesn't start with "{" 
        // because it was included in the assistant prefill message
        // Check for JSON property start (starts with ") or newline+property
        if (!jsonContent.StartsWith('{') && !jsonContent.StartsWith('['))
        {
            // If it starts with a quote (property name) or newline, add the opening brace
            jsonContent = "{" + jsonContent;
        }

        // Ensure the JSON ends properly
        jsonContent = jsonContent.Trim();
        if (!jsonContent.EndsWith('}') && !jsonContent.EndsWith(']'))
        {
            // If somehow missing closing brace
            if (jsonContent.StartsWith('{'))
                jsonContent += "}";
        }

        try
        {
            return JsonSerializer.Deserialize<TResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                Converters = { new CaseInsensitiveEnumConverterFactory(), new DateTimeConverterFactory() }
            }) ?? throw new JsonException("Deserialization returned null");
        }
        catch (JsonException ex)
        {
            // Add debug info to exception
            var preview = jsonContent.Length > 200 ? jsonContent[..200] + "..." : jsonContent;
            throw new JsonException($"Failed to parse JSON. First 200 chars: [{preview}]. Original error: {ex.Message}", ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
        }
    }

    /// <summary>
    /// Extracts JSON content from a response that may contain markdown or other text.
    /// Handles prefill technique where response doesn't start with "{".
    /// </summary>
    private static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var content = text.Trim();

        // If it already starts with { it's likely valid JSON object
        if (content.StartsWith('{'))
            return content;

        // PREFILL HANDLING: If content starts with " it's likely a JSON property name
        // This happens when using prefill technique where assistant starts with "{"
        // and Claude continues with the rest of the JSON object
        if (content.StartsWith('"'))
        {
            // This is continuation of JSON object - return as-is, caller will add {
            return content;
        }

        // If content starts with newline then ", it's also prefill continuation
        if (content.StartsWith("\n") || content.StartsWith("\r"))
        {
            var trimmed = content.TrimStart('\n', '\r', ' ', '\t');
            if (trimmed.StartsWith('"'))
                return trimmed;
        }

        // If it starts with [ it's a JSON array (but only at root level)
        if (content.StartsWith('['))
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
            // Skip any language identifier on the same line
            var newlinePos = content.IndexOf('\n', start);
            if (newlinePos > start)
                start = newlinePos + 1;
            var end = content.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return content[start..end].Trim();
        }

        // Try to find JSON object first (prefer { } over [ ])
        // This is important because the response may contain arrays as property values
        var firstBrace = content.IndexOf('{');
        var lastBrace = content.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return content[firstBrace..(lastBrace + 1)];

        // Try to find JSON array by locating first [ and last ]
        var firstBracket = content.IndexOf('[');
        var lastBracket = content.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
            return content[firstBracket..(lastBracket + 1)];

        // Return original if no JSON structure found
        return content;
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

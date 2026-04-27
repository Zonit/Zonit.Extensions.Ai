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

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Google Gemini provider implementation.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
[AiProvider("google")]
public sealed class GoogleProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleProvider> _logger;
    private readonly GoogleOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            GoogleJsonContext.Default,
            AiJsonTypeInfoResolver.Instance,
            new DefaultJsonTypeInfoResolver())
    };

    public GoogleProvider(
        HttpClient httpClient,
        IOptions<GoogleOptions> options,
        ILogger<GoogleProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "Google";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is GoogleBase;

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, GoogleJsonContext.Default.GeminiRequest);

        _logger.LogDebug("Google request: {Payload}", jsonPayload);

        var endpoint = $"/v1beta/models/{llm.Name}:generateContent?key={_options.ApiKey}";

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Google API failed: {response.StatusCode}: {responseJson}");
        }

        var geminiResponse = JsonSerializer.Deserialize(responseJson, GoogleJsonContext.Default.GeminiResponse)!;

        var textContent = geminiResponse.Candidates?.FirstOrDefault()?
            .Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Google response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0;
        var outputTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0;
        var reasoningTokens = geminiResponse.UsageMetadata?.ThoughtsTokenCount ?? 0;

        // Gemini thinking tokens are billed at the output token rate but are NOT
        // included in CandidatesTokenCount — add them to cost calculation separately.
        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens + reasoningTokens
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
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    ReasoningTokens = reasoningTokens,
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
        throw new NotSupportedException("Google Gemini image generation not yet implemented");
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Google Gemini does not support video generation");
    }

    /// <inheritdoc />
    public async Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = new GeminiEmbedRequest
        {
            Model = $"models/{llm.Name}",
            Content = new GeminiEmbedContent
            {
                Parts = new List<GeminiPartItem> { new() { Text = input } }
            }
        };

        var endpoint = $"/v1beta/models/{llm.Name}:embedContent?key={_options.ApiKey}";
        var jsonPayload = JsonSerializer.Serialize(request, GoogleJsonContext.Default.GeminiEmbedRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        stopwatch.Stop();
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var embeddingResponse = JsonSerializer.Deserialize(responseJson, GoogleJsonContext.Default.EmbeddingResponse);

        return new Result<float[]>
        {
            Value = embeddingResponse?.Embedding?.Values ?? [],
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "Embedding",
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage()
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
        var jsonPayload = JsonSerializer.Serialize(request, GoogleJsonContext.Default.GeminiRequest);

        var endpoint = $"/v1beta/models/{llm.Name}:streamGenerateContent?alt=sse&key={_options.ApiKey}";

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];
            var chunk = JsonSerializer.Deserialize(data, GoogleJsonContext.Default.GeminiResponse);

            var text = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (text != null)
                yield return text;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TResponse>> ChatAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildChatRequest(llm, prompt, chat, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, GoogleJsonContext.Default.GeminiRequest);
        _logger.LogDebug("Google chat request: {Payload}", jsonPayload);

        var endpoint = $"/v1beta/models/{llm.Name}:generateContent?key={_options.ApiKey}";

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google chat error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Google API failed: {response.StatusCode}: {responseJson}");
        }

        var geminiResponse = JsonSerializer.Deserialize(responseJson, GoogleJsonContext.Default.GeminiResponse)!;

        var textContent = geminiResponse.Candidates?.FirstOrDefault()?
            .Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Google response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0;
        var outputTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0;
        var reasoningTokens = geminiResponse.UsageMetadata?.ThoughtsTokenCount ?? 0;

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens + reasoningTokens
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
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    ReasoningTokens = reasoningTokens,
                    InputCost = inputCost,
                    OutputCost = outputCost
                }
            }
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(
        ILlm llm,
        IPrompt prompt,
        IReadOnlyList<ChatMessage> chat,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildChatRequest<string>(llm, new ChatFallback.PromptShim(prompt), chat, typeof(string));
        var jsonPayload = JsonSerializer.Serialize(request, GoogleJsonContext.Default.GeminiRequest);

        var endpoint = $"/v1beta/models/{llm.Name}:streamGenerateContent?alt=sse&key={_options.ApiKey}";

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];
            var chunk = JsonSerializer.Deserialize(data, GoogleJsonContext.Default.GeminiResponse);
            var text = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
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
        throw new NotSupportedException("Google audio transcription not yet implemented");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://generativelanguage.googleapis.com";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private static GeminiRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var parts = new List<GeminiPartItem> { new() { Text = prompt.Text } };

        if (prompt.Files != null)
        {
            foreach (var file in prompt.Files.Where(f => f.IsImage))
            {
                parts.Insert(0, new GeminiPartItem
                {
                    InlineData = new GeminiInlineData { MimeType = file.MediaType.Value, Data = file.Base64 }
                });
            }

            foreach (var file in prompt.Files.Where(f => f.IsDocument))
            {
                if (file.MediaType == Asset.MimeType.ApplicationPdf)
                {
                    parts.Insert(0, new GeminiPartItem
                    {
                        InlineData = new GeminiInlineData { MimeType = file.MediaType.Value, Data = file.Base64 }
                    });
                }
            }
        }

        var request = new GeminiRequest
        {
            Contents = new List<GeminiRequestContent> { new() { Parts = parts } }
        };

        var config = new GeminiGenerationConfig { MaxOutputTokens = llm.MaxTokens };

        if (llm is GoogleBase googleLlm)
        {
            if (googleLlm.Temperature < 1.0)
                config.Temperature = googleLlm.Temperature;
            if (googleLlm.TopP < 1.0)
                config.TopP = googleLlm.TopP;
        }

        if (responseType != typeof(string))
        {
            config.ResponseMimeType = "application/json";
            config.ResponseSchema = JsonSchemaGenerator.Generate(responseType);
        }

        request.GenerationConfig = config;

        if (!string.IsNullOrEmpty(prompt.System))
        {
            request.SystemInstruction = new GeminiSystemInstruction
            {
                Parts = new List<GeminiPartItem> { new() { Text = prompt.System } }
            };
        }

        return request;
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private static GeminiRequest BuildChatRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var contents = new List<GeminiRequestContent>();
        var sessionFilesAttached = false;

        foreach (var msg in chat)
        {
            switch (msg)
            {
                case User u:
                {
                    var parts = new List<GeminiPartItem>();
                    if (!sessionFilesAttached && prompt.Files != null)
                    {
                        AppendInlineFiles(parts, prompt.Files);
                        sessionFilesAttached = true;
                    }
                    if (u.Files != null) AppendInlineFiles(parts, u.Files);
                    parts.Add(new GeminiPartItem { Text = u.Text });
                    contents.Add(new GeminiRequestContent { Role = "user", Parts = parts });
                    break;
                }
                case Assistant a:
                    contents.Add(new GeminiRequestContent
                    {
                        Role = "model",
                        Parts = new List<GeminiPartItem> { new() { Text = a.Text } }
                    });
                    break;
                case Tool t:
                    contents.Add(new GeminiRequestContent
                    {
                        Role = "user",
                        Parts = new List<GeminiPartItem>
                        {
                            new()
                            {
                                FunctionResponse = new GeminiFunctionResponse
                                {
                                    Name = t.Name,
                                    Response = JsonSerializer.Deserialize(t.ResultJson, GoogleJsonContext.Default.JsonElement)
                                }
                            }
                        }
                    });
                    break;
            }
        }

        if (contents.Count == 0)
        {
            contents.Add(new GeminiRequestContent
            {
                Role = "user",
                Parts = new List<GeminiPartItem> { new() { Text = string.Empty } }
            });
        }

        var request = new GeminiRequest { Contents = contents };
        var config = new GeminiGenerationConfig { MaxOutputTokens = llm.MaxTokens };

        if (llm is GoogleBase googleLlm)
        {
            if (googleLlm.Temperature < 1.0) config.Temperature = googleLlm.Temperature;
            if (googleLlm.TopP < 1.0) config.TopP = googleLlm.TopP;
        }

        if (responseType != typeof(string))
        {
            config.ResponseMimeType = "application/json";
            config.ResponseSchema = JsonSchemaGenerator.Generate(responseType);
        }

        request.GenerationConfig = config;

        if (!string.IsNullOrEmpty(prompt.Text))
        {
            request.SystemInstruction = new GeminiSystemInstruction
            {
                Parts = new List<GeminiPartItem> { new() { Text = prompt.Text } }
            };
        }

        if (llm.Tools is { Length: > 0 } native)
        {
            var declarations = new List<GeminiFunctionDeclaration>();
            foreach (var t in native.OfType<FunctionTool>())
            {
                declarations.Add(new GeminiFunctionDeclaration
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.Parameters
                });
            }
            if (declarations.Count > 0)
                request.Tools = new List<GeminiToolGroup> { new() { FunctionDeclarations = declarations } };
        }

        return request;
    }

    private static void AppendInlineFiles(List<GeminiPartItem> parts, IReadOnlyList<Asset> files)
    {
        foreach (var file in files.Where(f => f.IsImage))
        {
            parts.Add(new GeminiPartItem { InlineData = new GeminiInlineData { MimeType = file.MediaType.Value, Data = file.Base64 } });
        }
        foreach (var file in files.Where(f => f.IsDocument && f.MediaType == Asset.MimeType.ApplicationPdf))
        {
            parts.Add(new GeminiPartItem { InlineData = new GeminiInlineData { MimeType = file.MediaType.Value, Data = file.Base64 } });
        }
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
internal sealed class GeminiResponse
{
    public GeminiCandidate[]? Candidates { get; set; }
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

internal sealed class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
}

internal sealed class GeminiContent
{
    public GeminiPart[]? Parts { get; set; }
}

internal sealed class GeminiPart
{
    public string? Text { get; set; }
}

internal sealed class GeminiUsageMetadata
{
    public int PromptTokenCount { get; set; }
    public int CandidatesTokenCount { get; set; }
    public int ThoughtsTokenCount { get; set; }
}

internal sealed class EmbeddingResponse
{
    public EmbeddingData? Embedding { get; set; }
}

internal sealed class EmbeddingData
{
    public float[]? Values { get; set; }
}

// Request models (AOT-safe DTO).
internal sealed class GeminiRequest
{
    public List<GeminiRequestContent> Contents { get; set; } = new();
    public GeminiGenerationConfig? GenerationConfig { get; set; }
    public GeminiSystemInstruction? SystemInstruction { get; set; }
    public List<GeminiToolGroup>? Tools { get; set; }
}

internal sealed class GeminiRequestContent
{
    public string? Role { get; set; }
    public List<GeminiPartItem> Parts { get; set; } = new();
}

internal sealed class GeminiPartItem
{
    public string? Text { get; set; }
    public GeminiInlineData? InlineData { get; set; }
    public GeminiFunctionResponse? FunctionResponse { get; set; }
    public GeminiFunctionCall? FunctionCall { get; set; }
}

internal sealed class GeminiFunctionCall
{
    public string Name { get; set; } = "";
    public JsonElement Args { get; set; }
}

internal sealed class GeminiInlineData
{
    public string MimeType { get; set; } = "";
    public string Data { get; set; } = "";
}

internal sealed class GeminiFunctionResponse
{
    public string Name { get; set; } = "";
    public JsonElement Response { get; set; }
}

internal sealed class GeminiGenerationConfig
{
    public int? MaxOutputTokens { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public string? ResponseMimeType { get; set; }
    public JsonElement? ResponseSchema { get; set; }
}

internal sealed class GeminiSystemInstruction
{
    public List<GeminiPartItem> Parts { get; set; } = new();
}

internal sealed class GeminiToolGroup
{
    public List<GeminiFunctionDeclaration>? FunctionDeclarations { get; set; }
}

internal sealed class GeminiFunctionDeclaration
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public JsonElement Parameters { get; set; }
}

internal sealed class GeminiEmbedRequest
{
    public string Model { get; set; } = "";
    public GeminiEmbedContent Content { get; set; } = new();
}

internal sealed class GeminiEmbedContent
{
    public List<GeminiPartItem> Parts { get; set; } = new();
}

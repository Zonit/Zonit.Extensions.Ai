using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// OpenAI provider implementation.
/// </summary>
[AiProvider("openai")]
public sealed class OpenAiProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly OpenAiOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAiProvider(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "OpenAI";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is OpenAiBase;

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

        _logger.LogDebug("OpenAI request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/responses", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"OpenAI API failed: {response.StatusCode}: {responseJson}");
        }

        var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseJson, JsonOptions)!;

        if (openAiResponse.Status != "completed")
            throw new InvalidOperationException($"OpenAI status: {openAiResponse.Status}");

        var textContent = openAiResponse.Output?
            .FirstOrDefault(o => o.Type == "message")?
            .Content?.FirstOrDefault(c => c.Type == "output_text")?.Text;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in OpenAI response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = openAiResponse.Usage?.InputTokens ?? 0;
        var outputTokens = openAiResponse.Usage?.OutputTokens ?? 0;
        var cachedTokens = openAiResponse.Usage?.InputTokensDetails?.CachedTokens ?? 0;
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
            RequestId = openAiResponse.Id,
            Usage = new TokenUsage
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CachedTokens = cachedTokens,
                ReasoningTokens = openAiResponse.Usage?.OutputTokensDetails?.ReasoningTokens ?? 0,
                InputCost = inputCost,
                OutputCost = outputCost
            }
        };
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async Task<AiResult<AiFile>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<AiFile> prompt,
        CancellationToken cancellationToken = default)
    {
        if (llm is not OpenAiImageBase imageLlm)
            throw new ArgumentException($"Expected OpenAI image model, got {llm.GetType().Name}");

        var stopwatch = Stopwatch.StartNew();

        var request = new
        {
            model = llm.Name,
            prompt = prompt.Text,
            n = 1,
            size = imageLlm.SizeValue,
            quality = imageLlm.QualityValue,
            response_format = "b64_json"
        };

        using var response = await _httpClient.PostAsJsonAsync("/v1/images/generations", request, cancellationToken);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var imageResponse = JsonSerializer.Deserialize<ImageResponse>(responseJson, JsonOptions);

        if (imageResponse?.Data == null || imageResponse.Data.Length == 0)
            throw new InvalidOperationException("No image data");

        var imageBytes = Convert.FromBase64String(imageResponse.Data[0].B64Json);
        var file = new AiFile
        {
            Name = "generated.png",
            MimeType = "image/png",
            Data = imageBytes
        };

        var imageCost = AiCostCalculator.CalculateImageCost(imageLlm);

        return new AiResult<AiFile>
        {
            Value = file,
            Model = llm.Name,
            Provider = Name,
            PromptName = prompt.GetType().Name.Replace("Prompt", ""),
            Duration = stopwatch.Elapsed,
            Usage = new TokenUsage
            {
                OutputCost = imageCost
            }
        };
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async Task<AiResult<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = new
        {
            model = llm.Name,
            input,
            dimensions = llm.Dimensions
        };

        using var response = await _httpClient.PostAsJsonAsync("/v1/embeddings", request, cancellationToken);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson, JsonOptions);

        if (embeddingResponse?.Data == null || embeddingResponse.Data.Length == 0)
            throw new InvalidOperationException("No embedding data");

        var inputTokens = embeddingResponse.Usage?.PromptTokens ?? 0;
        var embeddingCost = AiCostCalculator.CalculateEmbeddingCost(llm, inputTokens);

        return new AiResult<float[]>
        {
            Value = embeddingResponse.Data[0].Embedding,
            Model = llm.Name,
            Provider = Name,
            PromptName = "Embedding",
            Duration = stopwatch.Elapsed,
            Usage = new TokenUsage
            {
                InputTokens = inputTokens,
                InputCost = embeddingCost
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
        var request = BuildRequest(llm, prompt, typeof(TResponse), streaming: true);
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
            if (chunk?.Delta?.Text != null)
                yield return chunk.Delta.Text;
        }
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public async Task<AiResult<string>> TranscribeAsync(
        IAudioLlm llm,
        AiFile audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        using var formContent = new MultipartFormDataContent();
        formContent.Add(new ByteArrayContent(audioFile.Data), "file", audioFile.Name);
        formContent.Add(new StringContent(llm.Name), "model");

        if (language != null)
            formContent.Add(new StringContent(language), "language");

        using var response = await _httpClient.PostAsync("/v1/audio/transcriptions", formContent, cancellationToken);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(cancellationToken: cancellationToken);

        return new AiResult<string>
        {
            Value = result?.Text ?? "",
            Model = llm.Name,
            Provider = Name,
            PromptName = "Transcription",
            Duration = stopwatch.Elapsed,
            Usage = new TokenUsage()
        };
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.openai.com";
        _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        if (!string.IsNullOrEmpty(_options.OrganizationId))
        {
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", _options.OrganizationId);
        }
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    private Dictionary<string, object> BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        Type responseType,
        bool streaming = false)
    {
        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["max_output_tokens"] = llm.MaxTokens
        };

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
        request["messages"] = messages;

        // Structured output schema
        if (responseType != typeof(string))
        {
            var schema = JsonSchemaGenerator.Generate(responseType);
            request["response_format"] = new Dictionary<string, object>
            {
                ["type"] = "json_schema",
                ["json_schema"] = new Dictionary<string, object>
                {
                    ["name"] = "response",
                    ["description"] = JsonSchemaGenerator.GetDescription(responseType) ?? "Response",
                    ["schema"] = schema,
                    ["strict"] = true
                }
            };
        }

        if (streaming)
            request["stream"] = true;

        // Model-specific settings
        if (llm is OpenAiChatBase textLlm)
        {
            request["temperature"] = textLlm.Temperature;
            request["top_p"] = textLlm.TopP;
        }
        else if (llm is OpenAiReasoningBase { Reason: not null } reasoningLlm)
        {
            request["reasoning_effort"] = reasoningLlm.Reason.Value.ToString().ToLowerInvariant();
        }

        // Tools from LLM
        if (llm.Tools != null && llm.Tools.Length > 0)
        {
            request["tools"] = llm.Tools.Select(BuildToolRequest).ToList();
        }

        return request;
    }

    private static object BuildToolRequest(IToolBase tool) => tool switch
    {
        FunctionTool f => new
        {
            type = "function",
            function = new
            {
                name = f.Name,
                description = f.Description,
                parameters = f.Parameters,
                strict = f.Strict
            }
        },
        WebSearchTool w => new
        {
            type = "web_search_preview",
            web_search_preview = new
            {
                search_context_size = w.ContextSize.ToString().ToLowerInvariant()
            }
        },
        CodeInterpreterTool => new { type = "code_interpreter" },
        FileSearchTool fs => new
        {
            type = "file_search",
            file_search = new { max_num_results = fs.MaxNumResults ?? 20 }
        },
        _ => new { type = "unknown" }
    };

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    private static TResponse ParseResponse<TResponse>(string json)
    {
        if (typeof(TResponse) == typeof(string))
            return (TResponse)(object)json;

        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        var jsonToDeserialize = parsed.TryGetProperty("result", out var result)
            ? result.GetRawText()
            : json;

        return JsonSerializer.Deserialize<TResponse>(jsonToDeserialize, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        }) ?? throw new JsonException("Deserialization returned null");
    }
}

// Response models
internal sealed class OpenAiResponse
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public OpenAiOutput[]? Output { get; set; }
    public OpenAiUsage? Usage { get; set; }
}

internal sealed class OpenAiOutput
{
    public string? Type { get; set; }
    public OpenAiContent[]? Content { get; set; }
}

internal sealed class OpenAiContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

internal sealed class OpenAiUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public OpenAiTokenDetails? InputTokensDetails { get; set; }
    public OpenAiTokenDetails? OutputTokensDetails { get; set; }
}

internal sealed class OpenAiTokenDetails
{
    public int CachedTokens { get; set; }
    public int ReasoningTokens { get; set; }
}

internal sealed class ImageResponse
{
    public ImageData[]? Data { get; set; }
    public OpenAiUsage? Usage { get; set; }
}

internal sealed class ImageData
{
    public string B64Json { get; set; } = "";
}

internal sealed class EmbeddingResponse
{
    public EmbeddingData[]? Data { get; set; }
    public EmbeddingUsage? Usage { get; set; }
}

internal sealed class EmbeddingData
{
    public float[] Embedding { get; set; } = [];
}

internal sealed class EmbeddingUsage
{
    public int PromptTokens { get; set; }
}

internal sealed class StreamChunk
{
    public StreamDelta? Delta { get; set; }
}

internal sealed class StreamDelta
{
    public string? Text { get; set; }
}

internal sealed class TranscriptionResponse
{
    public string? Text { get; set; }
}

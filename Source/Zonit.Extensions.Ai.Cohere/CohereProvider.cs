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

namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Cohere provider implementation.
/// Supports chat completions and embeddings.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
[AiProvider("cohere")]
public sealed class CohereProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CohereProvider> _logger;
    private readonly CohereOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            CohereJsonContext.Default,
            AiJsonTypeInfoResolver.Instance,
            new DefaultJsonTypeInfoResolver())
    };

    public CohereProvider(
        HttpClient httpClient,
        IOptions<CohereOptions> options,
        ILogger<CohereProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "Cohere";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is CohereBase or CohereEmbeddingBase;

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, CohereJsonContext.Default.CohereChatRequest);

        _logger.LogDebug("Cohere request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v2/chat", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Cohere error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Cohere API failed: {response.StatusCode}: {responseJson}");
        }

        var cohereResponse = JsonSerializer.Deserialize(responseJson, CohereJsonContext.Default.CohereResponse)!;

        var textContent = cohereResponse.Message?.Content?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Cohere response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = cohereResponse.Usage?.Tokens?.InputTokens ?? 0;
        var outputTokens = cohereResponse.Usage?.Tokens?.OutputTokens ?? 0;
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
                RequestId = cohereResponse.Id,
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
        throw new NotSupportedException("Cohere does not support image generation");
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Cohere does not support video generation");
    }

    /// <inheritdoc />
    public async Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = new CohereEmbedRequest
        {
            Model = llm.Name,
            Texts = new List<string> { input },
            InputType = "search_document",
            EmbeddingTypes = new List<string> { "float" }
        };

        var jsonPayload = JsonSerializer.Serialize(request, CohereJsonContext.Default.CohereEmbedRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v2/embed", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Cohere embed error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Cohere API failed: {response.StatusCode}: {responseJson}");
        }

        var embedResponse = JsonSerializer.Deserialize(responseJson, CohereJsonContext.Default.CohereEmbedResponse)!;
        var embedding = embedResponse.Embeddings?.Float?.FirstOrDefault()
            ?? throw new InvalidOperationException("No embeddings in Cohere response");

        var inputTokens = embedResponse.Meta?.BilledUnits?.InputTokens ?? 0;
        var (inputCost, _) = AiCostCalculator.CalculateCosts(llm, new TokenUsage { InputTokens = inputTokens });

        return new Result<float[]>
        {
            Value = embedding,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "Embed",
                Duration = stopwatch.Elapsed,
                RequestId = embedResponse.Id,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    InputCost = inputCost
                }
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
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, CohereJsonContext.Default.CohereChatRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v2/chat") { Content = content };
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

            var chunk = JsonSerializer.Deserialize(data, CohereJsonContext.Default.CohereStreamChunk);
            if (chunk?.Type == "content-delta")
            {
                var text = chunk.Delta?.Message?.Content?.Text;
                if (text != null)
                    yield return text;
            }
        }
    }

    /// <inheritdoc />
    public Task<Result<string>> TranscribeAsync(
        IAudioLlm llm,
        Asset audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Cohere does not support audio transcription");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.cohere.com";
        _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private static CohereChatRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var messages = new List<CohereRequestMessage>();

        if (!string.IsNullOrEmpty(prompt.System))
            messages.Add(new CohereRequestMessage { Role = "system", Content = prompt.System });

        messages.Add(new CohereRequestMessage { Role = "user", Content = prompt.Text });

        var request = new CohereChatRequest
        {
            Model = llm.Name,
            Messages = messages
        };

        if (llm is CohereBase cohereLlm)
        {
            if (cohereLlm.Temperature < 1.0)
                request.Temperature = cohereLlm.Temperature;
            if (cohereLlm.TopP < 1.0)
                request.P = cohereLlm.TopP;
        }

        if (llm.MaxTokens > 0)
            request.MaxTokens = llm.MaxTokens;

        if (responseType != typeof(string))
        {
            request.ResponseFormat = new CohereResponseFormat
            {
                Type = "json_object",
                Schema = JsonSchemaGenerator.Generate(responseType)
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

        return JsonSerializer.Deserialize<TResponse>(jsonContent, JsonResponseParser.ProviderResponseOptions)
            ?? throw new JsonException("Deserialization returned null");
    }

    private static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var content = text.Trim();

        if (content.StartsWith('{') || content.StartsWith('['))
            return content;

        if (content.Contains("```json"))
        {
            var start = content.IndexOf("```json", StringComparison.Ordinal) + 7;
            var end = content.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return content[start..end].Trim();
        }

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

        var firstBrace = content.IndexOf('{');
        var lastBrace = content.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return content[firstBrace..(lastBrace + 1)];

        var firstBracket = content.IndexOf('[');
        var lastBracket = content.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
            return content[firstBracket..(lastBracket + 1)];

        return content;
    }
}

// Response models
internal sealed class CohereResponse
{
    public string? Id { get; set; }
    public CohereMessage? Message { get; set; }
    public CohereUsage? Usage { get; set; }
}

internal sealed class CohereMessage
{
    public CohereContent[]? Content { get; set; }
}

internal sealed class CohereContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

internal sealed class CohereUsage
{
    public CohereTokens? Tokens { get; set; }
}

internal sealed class CohereTokens
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

internal sealed class CohereEmbedResponse
{
    public string? Id { get; set; }
    public CohereEmbeddings? Embeddings { get; set; }
    public CohereMeta? Meta { get; set; }
}

internal sealed class CohereEmbeddings
{
    public float[][]? Float { get; set; }
}

internal sealed class CohereMeta
{
    public CohereBilledUnits? BilledUnits { get; set; }
}

internal sealed class CohereBilledUnits
{
    public int InputTokens { get; set; }
}

internal sealed class CohereStreamChunk
{
    public string? Type { get; set; }
    public CohereDelta? Delta { get; set; }
}

internal sealed class CohereDelta
{
    public CohereDeltaMessage? Message { get; set; }
}

internal sealed class CohereDeltaMessage
{
    public CohereDeltaContent? Content { get; set; }
}

internal sealed class CohereDeltaContent
{
    public string? Text { get; set; }
}

// Request models (AOT-safe DTO).
internal sealed class CohereChatRequest
{
    public string Model { get; set; } = "";
    public List<CohereRequestMessage> Messages { get; set; } = new();
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double? P { get; set; }
    public bool? Stream { get; set; }
    public CohereResponseFormat? ResponseFormat { get; set; }
}

internal sealed class CohereRequestMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal sealed class CohereResponseFormat
{
    public string Type { get; set; } = "";
    public JsonElement Schema { get; set; }
}

internal sealed class CohereEmbedRequest
{
    public string Model { get; set; } = "";
    public List<string> Texts { get; set; } = new();
    public string InputType { get; set; } = "";
    public List<string> EmbeddingTypes { get; set; } = new();
}

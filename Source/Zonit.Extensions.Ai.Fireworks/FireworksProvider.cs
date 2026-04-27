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

namespace Zonit.Extensions.Ai.Fireworks;

/// <summary>
/// Fireworks AI provider implementation.
/// Uses OpenAI-compatible API with fast inference.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
[AiProvider("fireworks")]
public sealed class FireworksProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FireworksProvider> _logger;
    private readonly FireworksOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            FireworksJsonContext.Default,
            AiJsonTypeInfoResolver.Instance,
            new DefaultJsonTypeInfoResolver())
    };

    public FireworksProvider(
        HttpClient httpClient,
        IOptions<FireworksOptions> options,
        ILogger<FireworksProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "Fireworks";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is FireworksBase;

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, FireworksJsonContext.Default.FireworksChatRequest);

        _logger.LogDebug("Fireworks request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/inference/v1/chat/completions", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Fireworks error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Fireworks API failed: {response.StatusCode}: {responseJson}");
        }

        var fireworksResponse = JsonSerializer.Deserialize(responseJson, FireworksJsonContext.Default.FireworksResponse)!;

        var textContent = fireworksResponse.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Fireworks response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = fireworksResponse.Usage?.PromptTokens ?? 0;
        var outputTokens = fireworksResponse.Usage?.CompletionTokens ?? 0;
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
                RequestId = fireworksResponse.Id,
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
        throw new NotSupportedException("Fireworks does not support image generation via this interface");
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Fireworks does not support video generation");
    }

    /// <inheritdoc />
    public Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Fireworks embeddings not implemented yet");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, FireworksJsonContext.Default.FireworksChatRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/inference/v1/chat/completions") { Content = content };
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

            var chunk = JsonSerializer.Deserialize(data, FireworksJsonContext.Default.FireworksStreamChunk);
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
        throw new NotSupportedException("Fireworks does not support audio transcription");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.fireworks.ai";
        _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private static FireworksChatRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var messages = new List<FireworksRequestMessage>();

        if (!string.IsNullOrEmpty(prompt.System))
            messages.Add(new FireworksRequestMessage { Role = "system", Content = prompt.System });

        messages.Add(new FireworksRequestMessage { Role = "user", Content = prompt.Text });

        var request = new FireworksChatRequest
        {
            Model = llm.Name,
            Messages = messages,
            MaxTokens = llm.MaxTokens
        };

        if (llm is FireworksBase fireworksLlm)
        {
            if (fireworksLlm.Temperature < 1.0)
                request.Temperature = fireworksLlm.Temperature;
            if (fireworksLlm.TopP < 1.0)
                request.TopP = fireworksLlm.TopP;
        }

        if (responseType != typeof(string))
        {
            request.ResponseFormat = new FireworksResponseFormat
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
internal sealed class FireworksResponse
{
    public string? Id { get; set; }
    public FireworksChoice[]? Choices { get; set; }
    public FireworksUsage? Usage { get; set; }
}

internal sealed class FireworksChoice
{
    public FireworksMessage? Message { get; set; }
}

internal sealed class FireworksMessage
{
    public string? Content { get; set; }
}

internal sealed class FireworksUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}

internal sealed class FireworksStreamChunk
{
    public FireworksStreamChoice[]? Choices { get; set; }
}

internal sealed class FireworksStreamChoice
{
    public FireworksStreamDelta? Delta { get; set; }
}

internal sealed class FireworksStreamDelta
{
    public string? Content { get; set; }
}

// Request models (AOT-safe DTO).
internal sealed class FireworksChatRequest
{
    public string Model { get; set; } = "";
    public List<FireworksRequestMessage> Messages { get; set; } = new();
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public bool? Stream { get; set; }
    public FireworksResponseFormat? ResponseFormat { get; set; }
}

internal sealed class FireworksRequestMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal sealed class FireworksResponseFormat
{
    public string Type { get; set; } = "";
    public JsonElement Schema { get; set; }
}

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

namespace Zonit.Extensions.Ai.Alibaba;

/// <summary>
/// Alibaba Cloud (DashScope) provider implementation.
/// Provides access to Qwen models.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
[AiProvider("alibaba")]
public sealed class AlibabaProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlibabaProvider> _logger;
    private readonly AlibabaOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            AlibabaJsonContext.Default,
            AiJsonTypeInfoResolver.Instance,
            new DefaultJsonTypeInfoResolver())
    };

    public AlibabaProvider(
        HttpClient httpClient,
        IOptions<AlibabaOptions> options,
        ILogger<AlibabaProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "Alibaba";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is AlibabaBase;

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, AlibabaJsonContext.Default.AlibabaChatRequest);

        _logger.LogDebug("Alibaba request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/compatible-mode/v1/chat/completions", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Alibaba error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"Alibaba API failed: {response.StatusCode}: {responseJson}");
        }

        var alibabaResponse = JsonSerializer.Deserialize(responseJson, AlibabaJsonContext.Default.AlibabaResponse)!;

        var textContent = alibabaResponse.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrEmpty(textContent))
            throw new InvalidOperationException("No text in Alibaba response");

        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = alibabaResponse.Usage?.PromptTokens ?? 0;
        var outputTokens = alibabaResponse.Usage?.CompletionTokens ?? 0;
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
                RequestId = alibabaResponse.Id,
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
        throw new NotSupportedException("Alibaba image generation not implemented via this interface");
    }

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Alibaba does not support video generation");
    }

    /// <inheritdoc />
    public Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Alibaba embeddings not implemented yet");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, AlibabaJsonContext.Default.AlibabaChatRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/compatible-mode/v1/chat/completions") { Content = content };
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

            var chunk = JsonSerializer.Deserialize(data, AlibabaJsonContext.Default.AlibabaStreamChunk);
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
        throw new NotSupportedException("Alibaba does not support audio transcription via this interface");
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://dashscope.aliyuncs.com";
        _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private static AlibabaChatRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var messages = new List<AlibabaRequestMessage>();

        if (!string.IsNullOrEmpty(prompt.System))
            messages.Add(new AlibabaRequestMessage { Role = "system", Content = prompt.System });

        messages.Add(new AlibabaRequestMessage { Role = "user", Content = prompt.Text });

        var request = new AlibabaChatRequest
        {
            Model = llm.Name,
            Messages = messages,
            MaxTokens = llm.MaxTokens
        };

        if (llm is AlibabaBase alibabaLlm)
        {
            if (alibabaLlm.Temperature < 1.0)
                request.Temperature = alibabaLlm.Temperature;
            if (alibabaLlm.TopP < 1.0)
                request.TopP = alibabaLlm.TopP;
        }

        if (responseType != typeof(string))
        {
            request.ResponseFormat = new AlibabaResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new AlibabaJsonSchemaSpec
                {
                    Name = "response",
                    Schema = JsonSchemaGenerator.Generate(responseType),
                    Strict = true
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
internal sealed class AlibabaResponse
{
    public string? Id { get; set; }
    public AlibabaChoice[]? Choices { get; set; }
    public AlibabaUsage? Usage { get; set; }
}

internal sealed class AlibabaChoice
{
    public AlibabaMessage? Message { get; set; }
}

internal sealed class AlibabaMessage
{
    public string? Content { get; set; }
}

internal sealed class AlibabaUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}

internal sealed class AlibabaStreamChunk
{
    public AlibabaStreamChoice[]? Choices { get; set; }
}

internal sealed class AlibabaStreamChoice
{
    public AlibabaStreamDelta? Delta { get; set; }
}

internal sealed class AlibabaStreamDelta
{
    public string? Content { get; set; }
}

// Request models (AOT-safe DTO).
internal sealed class AlibabaChatRequest
{
    public string Model { get; set; } = "";
    public List<AlibabaRequestMessage> Messages { get; set; } = new();
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public bool? Stream { get; set; }
    public AlibabaResponseFormat? ResponseFormat { get; set; }
}

internal sealed class AlibabaRequestMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal sealed class AlibabaResponseFormat
{
    public string Type { get; set; } = "";
    public AlibabaJsonSchemaSpec? JsonSchema { get; set; }
}

internal sealed class AlibabaJsonSchemaSpec
{
    public string Name { get; set; } = "";
    public JsonElement Schema { get; set; }
    public bool Strict { get; set; }
}

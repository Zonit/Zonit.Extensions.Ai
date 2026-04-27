using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
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

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// OpenAI provider implementation.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // AOT path first: provider DTOs -> user response types -> reflection
        // fallback (for dynamically-shaped payloads such as
        // Dictionary<string, object> requests).
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            OpenAiJsonContext.Default,
            AiJsonTypeInfoResolver.Instance,
            new DefaultJsonTypeInfoResolver())
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
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAiResponsesRequest);

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

        var openAiResponse = JsonSerializer.Deserialize(responseJson, OpenAiJsonContext.Default.OpenAiResponse)!;

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

        return new Result<TResponse>
        {
            Value = result,
            MetaData = new MetaData
            {
                Model = llm,
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
            }
        };
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
        var jsonPayload = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAiResponsesRequest);

        _logger.LogDebug("OpenAI chat request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/responses", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI chat error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"OpenAI API failed: {response.StatusCode}: {responseJson}");
        }

        var openAiResponse = JsonSerializer.Deserialize(responseJson, OpenAiJsonContext.Default.OpenAiResponse)!;

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

        return new Result<TResponse>
        {
            Value = result,
            MetaData = new MetaData
            {
                Model = llm,
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
            }
        };
    }

    /// <inheritdoc />
    public async Task<Result<Asset>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = new OpenAiImageRequest
        {
            Model = llm.Name,
            Prompt = prompt.Text,
            N = 1,
            Size = llm.SizeValue,
            Quality = llm.QualityValue
        };

        var imagePayload = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAiImageRequest);
        using var imageContent = new StringContent(imagePayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/images/generations", imageContent, cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenAI image generation error: {Status} - {Response}", response.StatusCode, errorContent);
            throw new HttpRequestException($"OpenAI Image API failed: {response.StatusCode}: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var imageResponse = JsonSerializer.Deserialize(responseJson, OpenAiJsonContext.Default.ImageResponse);

        if (imageResponse?.Data == null || imageResponse.Data.Length == 0)
            throw new InvalidOperationException("No image data");

        var imageBytes = Convert.FromBase64String(imageResponse.Data[0].B64Json);

        // Create Asset from generated image bytes
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
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("OpenAI does not support video generation");
    }

    /// <inheritdoc />
    public async Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = new OpenAiEmbedRequest
        {
            Model = llm.Name,
            Input = input,
            Dimensions = llm.Dimensions
        };

        var embedPayload = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAiEmbedRequest);
        using var embedContent = new StringContent(embedPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/embeddings", embedContent, cancellationToken);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var embeddingResponse = JsonSerializer.Deserialize(responseJson, OpenAiJsonContext.Default.EmbeddingResponse);

        if (embeddingResponse?.Data == null || embeddingResponse.Data.Length == 0)
            throw new InvalidOperationException("No embedding data");

        var inputTokens = embeddingResponse.Usage?.PromptTokens ?? 0;
        var embeddingCost = AiCostCalculator.CalculateEmbeddingCost(llm, inputTokens);

        return new Result<float[]>
        {
            Value = embeddingResponse.Data[0].Embedding,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "Embedding",
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    InputCost = embeddingCost
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
        var request = BuildRequest(llm, prompt, typeof(TResponse), streaming: true);
        var jsonPayload = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAiResponsesRequest);

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

            var chunk = JsonSerializer.Deserialize(data, OpenAiJsonContext.Default.StreamChunk);
            if (chunk?.Delta?.Text != null)
                yield return chunk.Delta.Text;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(
        ILlm llm,
        IPrompt prompt,
        IReadOnlyList<ChatMessage> chat,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildChatRequest<string>(llm, new ChatFallback.PromptShim(prompt), chat, typeof(string));
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAiResponsesRequest);
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

            var chunk = JsonSerializer.Deserialize(data, OpenAiJsonContext.Default.StreamChunk);
            if (chunk?.Delta?.Text != null)
                yield return chunk.Delta.Text;
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> TranscribeAsync(
        IAudioLlm llm,
        Asset audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        using var formContent = new MultipartFormDataContent();
        formContent.Add(new ByteArrayContent(audioFile.Data), "file", audioFile.OriginalName.Value);
        formContent.Add(new StringContent(llm.Name), "model");

        if (language != null)
            formContent.Add(new StringContent(language), "language");

        using var response = await _httpClient.PostAsync("/v1/audio/transcriptions", formContent, cancellationToken);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();

        var transcriptionJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize(transcriptionJson, OpenAiJsonContext.Default.TranscriptionResponse);

        return new Result<string>
        {
            Value = result?.Text ?? "",
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "Transcription",
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage()
            }
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

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type; FileSearchTool.Filters may use reflection.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type; FileSearchTool.Filters may use reflection.")]
    private static OpenAiResponsesRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType,
        bool streaming = false)
    {
        var request = new OpenAiResponsesRequest
        {
            Model = llm.Name,
            MaxOutputTokens = llm.MaxTokens
        };

        if (!string.IsNullOrEmpty(prompt.System))
            request.Instructions = prompt.System;

        var content = new List<OpenAiContentPart>
        {
            new() { Type = "input_text", Text = prompt.Text }
        };

        if (prompt.Files != null)
        {
            foreach (var file in prompt.Files)
            {
                if (file.IsImage)
                    content.Add(new OpenAiContentPart { Type = "input_image", ImageUrl = file.DataUrl });
                else if (file.IsDocument)
                    content.Add(new OpenAiContentPart { Type = "input_file", FileData = file.DataUrl, Filename = file.OriginalName.Value });
            }
        }

        request.Input.Add(new OpenAiInputItem { Role = "user", Content = content });

        if (responseType != typeof(string))
        {
            request.Text = new OpenAiTextConfig
            {
                Format = new OpenAiTextFormat
                {
                    Type = "json_schema",
                    Name = "response",
                    Description = JsonSchemaGenerator.GetDescription(responseType) ?? "Response",
                    Schema = JsonSchemaGenerator.Generate(responseType),
                    Strict = true
                }
            };
        }

        if (streaming)
            request.Stream = true;

        if (llm is OpenAiBase openAiBase && openAiBase.StoreLogs)
            request.Store = true;

        if (llm is OpenAiChatBase textLlm)
        {
            if (textLlm.Temperature < 1.0)
                request.Temperature = textLlm.Temperature;
            if (textLlm.TopP < 1.0)
                request.TopP = textLlm.TopP;
        }
        else if (llm is OpenAiReasoningBase reasoningLlm)
        {
            var reasoning = new OpenAiReasoningConfig();
            var hasReasoning = false;

            if (reasoningLlm.Reason.HasValue)
            {
                reasoning.Effort = reasoningLlm.Reason.Value.ToString().ToLowerInvariant();
                hasReasoning = true;
            }

            if (reasoningLlm.ReasonSummary.HasValue)
            {
                reasoning.Summary = reasoningLlm.ReasonSummary.Value.ToString().ToLowerInvariant();
                hasReasoning = true;
            }

            if (hasReasoning)
                request.Reasoning = reasoning;

            if (reasoningLlm.Verbosity.HasValue)
            {
                request.Text ??= new OpenAiTextConfig();
                request.Text.Verbosity = reasoningLlm.Verbosity.Value.ToString().ToLowerInvariant();
            }
        }

        if (llm.Tools != null && llm.Tools.Length > 0)
            request.Tools = llm.Tools.Select(BuildToolItem).ToList();

        return request;
    }

    [RequiresUnreferencedCode("FileSearchTool.Filters may be a user-supplied object that requires reflection-based serialization.")]
    [RequiresDynamicCode("FileSearchTool.Filters may be a user-supplied object that requires reflection-based serialization.")]
    private static OpenAiToolItem BuildToolItem(IToolBase tool) => tool switch
    {
        FunctionTool f => new OpenAiToolItem
        {
            Type = "function",
            Name = f.Name,
            Description = f.Description,
            Parameters = f.Parameters,
            Strict = f.Strict
        },
        WebSearchTool w => new OpenAiToolItem
        {
            Type = "web_search",
            SearchContextSize = w.ContextSize.ToString().ToLowerInvariant()
        },
        CodeInterpreterTool => new OpenAiToolItem { Type = "code_interpreter" },
        FileSearchTool fs => BuildFileSearchToolItem(fs),
        _ => new OpenAiToolItem { Type = "unknown" }
    };

    [RequiresUnreferencedCode("FileSearchTool.Filters may be a user-supplied object that requires reflection-based serialization.")]
    [RequiresDynamicCode("FileSearchTool.Filters may be a user-supplied object that requires reflection-based serialization.")]
    private static OpenAiToolItem BuildFileSearchToolItem(FileSearchTool fs)
    {
        var tool = new OpenAiToolItem { Type = "file_search" };

        if (!string.IsNullOrEmpty(fs.VectorId))
            tool.VectorStoreIds = new List<string> { fs.VectorId };

        if (fs.MaxNumResults.HasValue)
            tool.MaxNumResults = fs.MaxNumResults.Value;

        if (fs.RankingOptions != null)
        {
            var ranking = new OpenAiRankingOptions();
            var hasRanking = false;
            if (!string.IsNullOrEmpty(fs.RankingOptions.Ranker))
            {
                ranking.Ranker = fs.RankingOptions.Ranker;
                hasRanking = true;
            }
            if (fs.RankingOptions.ScoreThreshold.HasValue)
            {
                ranking.ScoreThreshold = fs.RankingOptions.ScoreThreshold.Value;
                hasRanking = true;
            }
            if (hasRanking)
                tool.RankingOptions = ranking;
        }

        if (fs.Filters != null)
            tool.Filters = JsonSerializer.SerializeToElement(fs.Filters);

        return tool;
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type; FileSearchTool.Filters may use reflection.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type; FileSearchTool.Filters may use reflection.")]
    private static OpenAiResponsesRequest BuildChatRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var request = new OpenAiResponsesRequest
        {
            Model = llm.Name,
            MaxOutputTokens = llm.MaxTokens
        };

        if (!string.IsNullOrEmpty(prompt.Text))
            request.Instructions = prompt.Text;

        var input = new List<OpenAiInputItem>();
        var sessionFilesAttached = false;

        foreach (var msg in chat)
        {
            switch (msg)
            {
                case User u:
                {
                    var userContent = new List<OpenAiContentPart>
                    {
                        new() { Type = "input_text", Text = u.Text }
                    };
                    if (!sessionFilesAttached && prompt.Files != null)
                    {
                        AppendFiles(userContent, prompt.Files);
                        sessionFilesAttached = true;
                    }
                    if (u.Files != null) AppendFiles(userContent, u.Files);
                    input.Add(new OpenAiInputItem { Role = "user", Content = userContent });
                    break;
                }
                case Assistant a:
                    input.Add(new OpenAiInputItem
                    {
                        Role = "assistant",
                        Content = new List<OpenAiContentPart> { new() { Type = "output_text", Text = a.Text } }
                    });
                    break;
                case Tool t:
                    input.Add(new OpenAiInputItem
                    {
                        Type = "function_call_output",
                        CallId = t.ToolCallId,
                        Output = t.ResultJson
                    });
                    break;
            }
        }

        if (input.Count == 0)
        {
            input.Add(new OpenAiInputItem
            {
                Role = "user",
                Content = new List<OpenAiContentPart> { new() { Type = "input_text", Text = string.Empty } }
            });
        }

        request.Input = input;

        if (responseType != typeof(string))
        {
            request.Text = new OpenAiTextConfig
            {
                Format = new OpenAiTextFormat
                {
                    Type = "json_schema",
                    Name = "response",
                    Description = JsonSchemaGenerator.GetDescription(responseType) ?? "Response",
                    Schema = JsonSchemaGenerator.Generate(responseType),
                    Strict = true
                }
            };
        }

        if (llm is OpenAiBase openAiBase && openAiBase.StoreLogs)
            request.Store = true;

        if (llm is OpenAiChatBase textLlm)
        {
            if (textLlm.Temperature < 1.0) request.Temperature = textLlm.Temperature;
            if (textLlm.TopP < 1.0) request.TopP = textLlm.TopP;
        }
        else if (llm is OpenAiReasoningBase reasoningLlm)
        {
            var reasoning = new OpenAiReasoningConfig();
            var hasReasoning = false;
            if (reasoningLlm.Reason.HasValue)
            {
                reasoning.Effort = reasoningLlm.Reason.Value.ToString().ToLowerInvariant();
                hasReasoning = true;
            }
            if (reasoningLlm.ReasonSummary.HasValue)
            {
                reasoning.Summary = reasoningLlm.ReasonSummary.Value.ToString().ToLowerInvariant();
                hasReasoning = true;
            }
            if (hasReasoning) request.Reasoning = reasoning;

            if (reasoningLlm.Verbosity.HasValue)
            {
                request.Text ??= new OpenAiTextConfig();
                request.Text.Verbosity = reasoningLlm.Verbosity.Value.ToString().ToLowerInvariant();
            }
        }

        if (llm.Tools != null && llm.Tools.Length > 0)
            request.Tools = llm.Tools.Select(BuildToolItem).ToList();

        return request;
    }

    private static void AppendFiles(List<OpenAiContentPart> content, IReadOnlyList<Asset> files)
    {
        foreach (var file in files)
        {
            if (file.IsImage)
                content.Add(new OpenAiContentPart { Type = "input_image", ImageUrl = file.DataUrl });
            else if (file.IsDocument)
                content.Add(new OpenAiContentPart { Type = "input_file", FileData = file.DataUrl, Filename = file.OriginalName.Value });
        }
    }

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

        return JsonSerializer.Deserialize<TResponse>(jsonToDeserialize, JsonResponseParser.ProviderResponseOptions)
            ?? throw new JsonException("Deserialization returned null");
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

// Request models (AOT-safe DTO).
internal sealed class OpenAiResponsesRequest
{
    public string Model { get; set; } = "";
    public int? MaxOutputTokens { get; set; }
    public string? Instructions { get; set; }
    public string? PreviousResponseId { get; set; }
    public List<OpenAiInputItem> Input { get; set; } = new();
    public OpenAiTextConfig? Text { get; set; }
    public bool? Stream { get; set; }
    public bool? Store { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public OpenAiReasoningConfig? Reasoning { get; set; }
    public List<OpenAiToolItem>? Tools { get; set; }
}

internal sealed class OpenAiInputItem
{
    public string? Role { get; set; }
    public List<OpenAiContentPart>? Content { get; set; }
    public string? Type { get; set; }
    public string? CallId { get; set; }
    public string? Output { get; set; }
}

internal sealed class OpenAiContentPart
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public string? ImageUrl { get; set; }
    public string? FileData { get; set; }
    public string? Filename { get; set; }
}

internal sealed class OpenAiTextConfig
{
    public OpenAiTextFormat? Format { get; set; }
    public string? Verbosity { get; set; }
}

internal sealed class OpenAiTextFormat
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public JsonElement Schema { get; set; }
    public bool Strict { get; set; }
}

internal sealed class OpenAiReasoningConfig
{
    public string? Effort { get; set; }
    public string? Summary { get; set; }
}

internal sealed class OpenAiToolItem
{
    public string Type { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }
    public JsonElement? Parameters { get; set; }
    public bool? Strict { get; set; }
    public string? SearchContextSize { get; set; }
    public List<string>? VectorStoreIds { get; set; }
    public int? MaxNumResults { get; set; }
    public OpenAiRankingOptions? RankingOptions { get; set; }
    public JsonElement? Filters { get; set; }
}

internal sealed class OpenAiRankingOptions
{
    public string? Ranker { get; set; }
    public double? ScoreThreshold { get; set; }
}

internal sealed class OpenAiImageRequest
{
    public string Model { get; set; } = "";
    public string Prompt { get; set; } = "";
    public int N { get; set; }
    public string? Size { get; set; }
    public string? Quality { get; set; }
}

internal sealed class OpenAiEmbedRequest
{
    public string Model { get; set; } = "";
    public string Input { get; set; } = "";
    public int? Dimensions { get; set; }
}

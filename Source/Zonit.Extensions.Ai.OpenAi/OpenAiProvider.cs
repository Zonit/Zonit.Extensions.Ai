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
/// <remarks>
/// Structured output is AOT-safe on the documented <see cref="PromptBase{TResponse}"/>
/// path: the request schema comes from the build-time <c>AiSchemaRegistry</c> and the
/// response is deserialized through a source-generated <c>JsonTypeInfo&lt;TResponse&gt;</c>.
/// No class-level trim/AOT suppression is needed — the only reflection touchpoints are
/// genuinely-gated fallbacks that live behind their own annotations.
/// </remarks>
[AiProvider("openai")]
public sealed class OpenAiProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly OpenAiOptions _options;
    private readonly TimeSpan _interEventTimeout;

    /// <param name="httpClient">Typed client carrying the streaming resilience pipeline.</param>
    /// <param name="options">Provider options (key, organization, base URL).</param>
    /// <param name="logger">Provider logger.</param>
    /// <param name="aiOptions">
    /// Global AI options, for the stream watchdog. Optional so the constructor stays
    /// source-compatible; DI always supplies it (<c>AddAi()</c> registers it).
    /// </param>
    public OpenAiProvider(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiProvider> logger,
        IOptions<AiOptions>? aiOptions = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        // Dead-stream watchdog for the assembled (non-live) path — the same knob the
        // agent loop uses, so both streaming paths stall-detect identically.
        var configured = aiOptions?.Value.Resilience.InterEventTimeout ?? TimeSpan.Zero;
        _interEventTimeout = configured > TimeSpan.Zero ? configured : TimeSpan.FromMinutes(30);

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

        var openAiResponse = await SendResponsesAsync(llm, request, "GenerateAsync", cancellationToken);

        stopwatch.Stop();

        var textContent = ExtractText(llm, openAiResponse, "GenerateAsync");
        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = openAiResponse.Usage?.InputTokens ?? 0;
        var outputTokens = openAiResponse.Usage?.OutputTokens ?? 0;
        var cachedTokens = openAiResponse.Usage?.InputTokensDetails?.CachedTokens ?? 0;

        // Fast mode is best-effort: bill the tier OpenAI actually served, not the one asked for.
        var fastGranted = AiFastTier.WasGranted(llm, openAiResponse.ServiceTier, _logger, Name, "GenerateAsync");

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cachedTokens
        }, fastGranted);

        return new Result<TResponse>
        {
            Value = result,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = PromptNameResolver.Resolve(prompt),
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

        var openAiResponse = await SendResponsesAsync(llm, request, "ChatAsync", cancellationToken);

        stopwatch.Stop();

        var textContent = ExtractText(llm, openAiResponse, "ChatAsync");
        var result = ParseResponse<TResponse>(textContent);

        var inputTokens = openAiResponse.Usage?.InputTokens ?? 0;
        var outputTokens = openAiResponse.Usage?.OutputTokens ?? 0;
        var cachedTokens = openAiResponse.Usage?.InputTokensDetails?.CachedTokens ?? 0;

        // Fast mode is best-effort: bill the tier OpenAI actually served, not the one asked for.
        var fastGranted = AiFastTier.WasGranted(llm, openAiResponse.ServiceTier, _logger, Name, "ChatAsync");

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cachedTokens
        }, fastGranted);

        return new Result<TResponse>
        {
            Value = result,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = PromptNameResolver.Resolve(prompt),
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

        return new Result<Asset>
        {
            Value = generatedImage,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = PromptNameResolver.Resolve(prompt),
                Duration = stopwatch.Elapsed,
                Usage = BuildImageUsage(llm, imageResponse.Usage)
            }
        };
    }

    /// <summary>
    /// Builds the usage/cost block for an image generation. Models billed per
    /// token (gpt-image-2 and newer) are priced from the endpoint's own token
    /// counts; the older flat-rate models keep their per-image price.
    /// </summary>
    private static TokenUsage BuildImageUsage(IImageLlm llm, OpenAiUsage? usage)
    {
        if (llm is not ITokenPricedImageLlm tokenPriced || usage is null)
            return new TokenUsage { OutputCost = llm.GetImageGenerationPrice() };

        // input_tokens_details omits the split on some responses — fall back to
        // treating the whole prompt as text so nothing is billed at zero.
        var textInput = usage.InputTokensDetails?.TextTokens ?? usage.InputTokens;
        var imageInput = usage.InputTokensDetails?.ImageTokens ?? 0;
        if (textInput + imageInput == 0)
            textInput = usage.InputTokens;

        var imageUsage = new ImageTokenUsage
        {
            TextInputTokens = textInput,
            ImageInputTokens = imageInput,
            // The endpoint reports a single cached count; it applies to the text
            // prefix, which is the only cacheable part of an image request.
            CachedTextInputTokens = usage.InputTokensDetails?.CachedTokens ?? 0,
            ImageOutputTokens = usage.OutputTokensDetails?.ImageTokens is > 0
                ? usage.OutputTokensDetails.ImageTokens
                : usage.OutputTokens
        };

        var (inputCost, outputCost) = AiCostCalculator.CalculateImageTokenCosts(tokenPriced, imageUsage);

        return new TokenUsage
        {
            InputTokens = imageUsage.InputTokens,
            OutputTokens = imageUsage.ImageOutputTokens,
            CachedTokens = imageUsage.CachedTokens,
            InputCost = inputCost,
            OutputCost = outputCost
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

            if (ResponsesStreamAssembler.TryReadTextDelta(data) is { } fragment)
                yield return fragment;
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

            if (ResponsesStreamAssembler.TryReadTextDelta(data) is { } fragment)
                yield return fragment;
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

        // OpenAI identifies the audio format from the file extension and/or the
        // part's Content-Type. OriginalName can lack an extension (e.g. a Telegram
        // voice note saved as "file_123"), which makes the API reject the upload
        // with a 400. UniqueName is "{Id}{Extension}" with the extension derived
        // from the detected MediaType, and we also stamp Content-Type — so the
        // format is recognizable even when the original name has no extension.
        var fileContent = new ByteArrayContent(audioFile.Data);
        if (!string.IsNullOrEmpty(audioFile.MediaType.Value))
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(audioFile.MediaType.Value);

        var fileName = audioFile.Extension.Length > 0 ? audioFile.UniqueName : audioFile.OriginalName.Value;
        formContent.Add(fileContent, "file", fileName);
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

    /// <summary>
    /// Issues one <c>POST /v1/responses</c> and returns the finished response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Goes out as <c>stream: true</c> and comes back reassembled into the complete
    /// <see cref="OpenAiResponse"/> the caller expects (see
    /// <see cref="ResponsesApiTransport"/>, which also handles the buffered fallback for
    /// accounts that may not stream) — the contract of <c>GenerateAsync</c> / <c>ChatAsync</c>
    /// ("one call, one finished result") is unchanged; only the transport differs.
    /// </para>
    /// <para>
    /// The buffered form this replaces held one HTTP response open for the entire
    /// generation. With <c>max_output_tokens</c> defaulting to the model's full output
    /// capacity (128k on the GPT-5.6 tiers), a large structured answer can legitimately
    /// run past the per-attempt timeout, whereupon Polly cancelled it and retried —
    /// restarting generation from zero at full cost. Streaming removes the ceiling:
    /// frames arrive continuously, so liveness is what is measured instead of total
    /// duration (see <see cref="ResponsesStreamAssembler"/>).
    /// </para>
    /// </remarks>
    private async Task<OpenAiResponse> SendResponsesAsync(
        ILlm llm,
        OpenAiResponsesRequest request,
        string operation,
        CancellationToken cancellationToken)
    {
        var responseJson = await ResponsesApiTransport.SendAsync(
            _httpClient,
            "/v1/responses",
            streaming =>
            {
                // Wire-level only: the caller still receives one assembled response. Rebuilt per
                // attempt so the buffered fallback can drop the flag if the API rejects streaming.
                request.Stream = streaming ? true : null;
                return JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAiResponsesRequest);
            },
            _interEventTimeout,
            Name,
            operation,
            _logger,
            cancellationToken);

        return JsonSerializer.Deserialize(responseJson, OpenAiJsonContext.Default.OpenAiResponse)!;
    }

    /// <summary>
    /// Returns the answer text, or throws the diagnosis. A non-<c>completed</c> status
    /// and an empty output are the two ways the endpoint says "no answer"; both name
    /// the reason the API gave, because "OpenAI status: incomplete" alone sent callers
    /// hunting for a fault that the response had already explained.
    /// </summary>
    private string ExtractText(ILlm llm, OpenAiResponse response, string operation)
    {
        var text = response.Output?
            .FirstOrDefault(o => o.Type == "message")?
            .Content?.FirstOrDefault(c => c.Type == "output_text")?.Text;

        var reason = response.IncompleteDetails?.Reason;

        if (response.Status is not null && response.Status != "completed")
        {
            // `incomplete` with reason `max_output_tokens` is the common one: the model
            // spent its whole budget (usually on reasoning) before emitting an answer.
            // Not transient — retrying re-truncates — so it is classified as such.
            if (response.Status == "incomplete" && reason == "max_output_tokens")
            {
                throw new AiEmptyResponseException(
                    AiResponseError.Truncated,
                    $"OpenAI {operation} hit max_output_tokens on '{llm.Name}' before producing an answer. "
                    + $"Raise MaxTokens (currently {llm.MaxTokens:N0}) or lower the reasoning effort.",
                    stopReason: reason);
            }

            if (response.Status == "incomplete" && reason == "content_filter")
            {
                throw new AiEmptyResponseException(
                    AiResponseError.Refusal,
                    $"OpenAI {operation} was stopped by the content filter on '{llm.Name}'. Revise the prompt or inputs.",
                    stopReason: reason);
            }

            var detail = response.Error?.Message ?? reason;
            throw new InvalidOperationException(
                $"OpenAI {operation} returned status '{response.Status}' on '{llm.Name}'"
                + (detail is null ? "." : $": {detail}"));
        }

        if (string.IsNullOrEmpty(text))
        {
            throw new AiEmptyResponseException(
                AiResponseError.EmptyAfterRetries,
                $"OpenAI {operation} returned no text on '{llm.Name}' (status '{response.Status ?? "unknown"}'"
                + (reason is null ? "" : $", reason '{reason}'")
                + ") — server-side data loss; usually transient, re-run the operation.",
                stopReason: reason);
        }

        return text;
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
                    Schema = AiSchemaRegistry.GetSchema(responseType),
                    Strict = true
                }
            };
        }

        if (streaming)
            request.Stream = true;

        if (llm is OpenAiBase openAiBase && openAiBase.StoreLogs)
            request.Store = true;

        ApplyServiceTier(llm, request);

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

            var effort = ((IReasoningLlm)reasoningLlm).Reason;
            if (effort.HasValue)
            {
                reasoning.Effort = OpenAiReasoningBase.EffortToWire(effort.Value);
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

        if (llm is OpenAiBase ob && ob.Tools is { Length: > 0 } typedTools)
            request.Tools = BuildValidatedTools(llm, typedTools);

        return request;
    }

    /// <summary>
    /// Opts the request into fast mode when the model asks for it
    /// (<see cref="IFast"/> with <see cref="SpeedType.Fast"/>): up to ~2.5× faster
    /// output and steadier latency, billed at double the standard rate.
    /// </summary>
    /// <remarks>
    /// The wire field is <c>service_tier</c>. OpenAI renamed priority processing to
    /// fast mode on 30 July 2026 and kept <c>"priority"</c> as an accepted alias; we
    /// send the current spelling. Omitting the field entirely (standard processing)
    /// is the default for every model, including those that implement
    /// <see cref="IFast"/> but were left at <see cref="SpeedType.Standard"/>.
    /// </remarks>
    private static void ApplyServiceTier(ILlm llm, OpenAiResponsesRequest request)
    {
        if (llm is IFast { Speed: SpeedType.Fast })
            request.ServiceTier = "fast";
    }

    /// <summary>
    /// Materialises every entry on <see cref="OpenAiBase.Tools"/> as the
    /// matching Responses API tool block. Each case validates against the
    /// model's <see cref="ILlm.SupportedTools"/> mask first so a model whose
    /// declared capabilities exclude (say) <c>WebSearch</c> fails the request
    /// build with a clear error instead of an opaque API 400.
    /// </summary>
    internal static List<OpenAiToolItem> BuildValidatedTools(ILlm llm, IReadOnlyList<Tools.IOpenAiTool> tools)
    {
        var result = new List<OpenAiToolItem>(tools.Count);
        foreach (var t in tools)
            result.Add(BuildToolItem(llm, t));
        return result;
    }

    internal static OpenAiToolItem BuildToolItem(ILlm llm, Tools.IOpenAiTool tool) => tool switch
    {
        Tools.FunctionTool f => new OpenAiToolItem
        {
            Type = "function",
            Name = f.Name,
            Description = f.Description,
            Parameters = f.Parameters,
            Strict = f.Strict,
        },
        Tools.WebSearchTool w => RequireFlag(llm, ToolsType.WebSearch, w) is { } _
            ? new OpenAiToolItem
            {
                Type = "web_search",
                SearchContextSize = w.ContextSize.ToString().ToLowerInvariant(),
            }
            : throw new InvalidOperationException("unreachable"),
        Tools.CodeInterpreterTool ci when RequireFlag(llm, ToolsType.CodeInterpreter, ci) is var _
            => new OpenAiToolItem { Type = "code_interpreter" },
        Tools.FileSearchTool fs when RequireFlag(llm, ToolsType.FileSearch, fs) is var _
            => BuildFileSearchToolItem(fs),
        _ => throw new NotSupportedException(
            $"OpenAI provider does not yet wire tool '{tool.GetType().FullName}'."),
    };

    /// <summary>
    /// Asserts the model advertises <paramref name="required"/> in its
    /// <see cref="ILlm.SupportedTools"/> mask; throws otherwise. Returns the
    /// flag itself so the caller can chain it inside a switch expression
    /// pattern guard.
    /// </summary>
    private static ToolsType RequireFlag(ILlm llm, ToolsType required, IToolBase tool)
    {
        if (!llm.SupportedTools.HasFlag(required))
        {
            throw new NotSupportedException(
                $"Model '{llm.Name}' does not support tool '{tool.GetType().Name}' "
                + $"(required capability: {required}). The model advertises "
                + $"SupportedTools = {llm.SupportedTools}. Pick a model that lists "
                + $"the required flag, or remove the tool from llm.Tools.");
        }
        return required;
    }

    private static OpenAiToolItem BuildFileSearchToolItem(Tools.FileSearchTool fs)
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

        if (fs.Filters is { } filters)
            tool.Filters = filters;

        return tool;
    }

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
                    Schema = AiSchemaRegistry.GetSchema(responseType),
                    Strict = true
                }
            };
        }

        if (llm is OpenAiBase openAiBase && openAiBase.StoreLogs)
            request.Store = true;

        ApplyServiceTier(llm, request);

        if (llm is OpenAiChatBase textLlm)
        {
            if (textLlm.Temperature < 1.0) request.Temperature = textLlm.Temperature;
            if (textLlm.TopP < 1.0) request.TopP = textLlm.TopP;
        }
        else if (llm is OpenAiReasoningBase reasoningLlm)
        {
            var reasoning = new OpenAiReasoningConfig();
            var hasReasoning = false;
            var effort = ((IReasoningLlm)reasoningLlm).Reason;
            if (effort.HasValue)
            {
                reasoning.Effort = OpenAiReasoningBase.EffortToWire(effort.Value);
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

        if (llm is OpenAiBase ob && ob.Tools is { Length: > 0 } typedTools)
            request.Tools = BuildValidatedTools(llm, typedTools);

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

    private static TResponse ParseResponse<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(string json)
        => JsonResponseParser.DeserializeStructured<TResponse>(json);
}

// Response models
internal sealed class OpenAiResponse
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public OpenAiOutput[]? Output { get; set; }
    public OpenAiUsage? Usage { get; set; }

    /// <summary>Why a non-<c>completed</c> response stopped (e.g. <c>max_output_tokens</c>, <c>content_filter</c>).</summary>
    public OpenAiIncompleteDetails? IncompleteDetails { get; set; }

    /// <summary>Set on a <c>failed</c> response; carries the server-side fault.</summary>
    public OpenAiErrorDetail? Error { get; set; }

    /// <summary>
    /// Tier the request was actually served on. <c>"fast"</c> / <c>"priority"</c> confirm
    /// fast mode was granted; <c>"default"</c> means it was downgraded to standard
    /// processing (see <see cref="OpenAiProvider.ApplyServiceTier"/>).
    /// </summary>
    public string? ServiceTier { get; set; }
}

internal sealed class OpenAiIncompleteDetails
{
    public string? Reason { get; set; }
}

internal sealed class OpenAiErrorDetail
{
    public string? Code { get; set; }
    public string? Message { get; set; }
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

    // Only populated by the images endpoints, which split both sides of the
    // request into text and image tokens (each billed at its own rate).
    public int TextTokens { get; set; }
    public int ImageTokens { get; set; }
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
    public string? ServiceTier { get; set; }
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

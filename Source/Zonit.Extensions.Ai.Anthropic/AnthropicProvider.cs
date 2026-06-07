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

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Anthropic Claude provider implementation.
/// </summary>
/// <remarks>
/// Structured output is AOT-safe on the documented <see cref="PromptBase{TResponse}"/>
/// path: the request schema comes from the build-time <c>AiSchemaRegistry</c> and the
/// response is deserialized through a source-generated <c>JsonTypeInfo&lt;TResponse&gt;</c>.
/// No class-level trim/AOT suppression is needed — the only reflection touchpoints are
/// genuinely-gated fallbacks that live behind their own annotations.
/// </remarks>
[AiProvider("anthropic")]
public sealed class AnthropicProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicProvider> _logger;
    private readonly AnthropicOptions _options;

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

    /// <summary>
    /// Applies the model's thinking configuration to <paramref name="request"/>.
    /// <para>
    /// Two payload shapes:
    /// <list type="bullet">
    ///   <item><description><b>Adaptive</b> (Sonnet 4.6+, Opus 4.7+ — i.e. <see cref="AnthropicAdaptiveBase"/>):
    ///   sends <c>thinking.type = "adaptive"</c> together with a sibling
    ///   <c>output_config.effort</c> hint (<c>low|medium|high|xhigh|max</c>).
    ///   No <c>budget_tokens</c>.</description></item>
    ///   <item><description><b>Legacy enabled</b> (Sonnet 4.5, Opus 4.5/4.6 with explicit
    ///   <see cref="AnthropicLegacyThinkingBase.ThinkingBudget"/>): sends
    ///   <c>thinking.type = "enabled"</c> with a numeric <c>budget_tokens</c>.</description></item>
    ///   <item><description><b>Disabled</b>: <c>thinking</c> stays unset.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Returns the minimum <c>max_tokens</c> the request needs:
    /// <list type="bullet">
    ///   <item><description><b>Legacy enabled</b>: <c>budget + 1024</c> (Anthropic
    ///   requires <c>max_tokens &gt; budget_tokens</c>).</description></item>
    ///   <item><description><b>Adaptive</b>: an effort-dependent floor matching
    ///   Anthropic's published recommendations. Adaptive thinking tokens count
    ///   <i>toward</i> <c>max_tokens</c>, so the legacy 1024-token default
    ///   leaves no room for thinking + answer at medium/high effort and the
    ///   model returns <c>stop_reason="max_tokens"</c> with an empty <c>text</c>
    ///   block. The Anthropic Sonnet 4.6 migration guide recommends 16384 at
    ///   medium and 64000 at high effort; we mirror those thresholds, capped
    ///   by <see cref="ILlm.MaxOutputTokens"/> for models with smaller
    ///   ceilings.</description></item>
    ///   <item><description><b>Disabled</b>: <c>0</c>.</description></item>
    /// </list>
    /// The caller raises <c>request.MaxTokens</c> to the returned floor only
    /// when it would otherwise be lower, so users who explicitly set a higher
    /// budget keep their value.
    /// </para>
    /// </summary>
    /// <seealso href="https://platform.claude.com/docs/en/build-with-claude/adaptive-thinking">Adaptive thinking — Important considerations / Cost control</seealso>
    /// <seealso href="https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices">Migrating from Sonnet 4.5 to Sonnet 4.6</seealso>
    internal static int ApplyThinking(ILlm llm, AnthropicMessagesRequest request)
    {
        // Adaptive path: sonnet 4.6 / opus 4.7 / future models.
        if (llm is AnthropicAdaptiveBase
            && llm is IReasoningLlm rl
            && rl.Reason is { } effort
            && effort != ReasoningEffort.None)
        {
            request.Thinking = new AnthropicThinking { Type = "adaptive" };
            request.OutputConfig = new AnthropicOutputConfig { Effort = EffortToWire(effort) };

            // Adaptive thinking counts toward max_tokens. Anthropic's
            // canonical samples and migration guide for Sonnet 4.6 use
            // max_tokens=16384 at medium effort and 64000 at high effort.
            // Below the floor the model frequently exhausts the budget on
            // thinking alone and returns stop_reason="max_tokens" with no
            // text — surfacing in the agent loop as a silently empty turn
            // ("Reason=Medium just stops working" / "raz działa, raz nie").
            return effort switch
            {
                // Low effort thinks briefly; the default MaxTokens (now the
                // model's full output capacity) already leaves ample room.
                ReasoningEffort.Low => 0,
                ReasoningEffort.Medium => Math.Min(16_384, llm.MaxOutputTokens),
                // high / xhigh / max — grant the model its full output capacity
                // as headroom so deep thinking can run and still leave room to
                // act across tool calls. For Opus this is 128k, not a 64k cap.
                _ => llm.MaxOutputTokens,
            };
        }

        // Legacy fixed-budget path: Sonnet 4.5, Opus 4.5/4.6, Haiku 4.5.
        if (llm is AnthropicLegacyThinkingBase legacy && legacy.ThinkingBudget is { } budget)
        {
            request.Thinking = new AnthropicThinking { Type = "enabled", BudgetTokens = budget };
            // Anthropic requires max_tokens > budget_tokens; reserve 1024 tokens for the answer.
            return budget + 1024;
        }

        return 0;
    }

    /// <summary>
    /// Applies prompt caching to <paramref name="request"/> when the model is an
    /// <see cref="AnthropicBase"/> with <see cref="AnthropicBase.Cache"/>
    /// enabled. Uses all four available <c>cache_control</c> breakpoints to
    /// implement a true <i>rolling</i> cache for agent / chat loops:
    /// <list type="number">
    ///   <item><description><b>tools[last]</b> — caches the entire tool catalogue (highest leverage for agents with many tools).</description></item>
    ///   <item><description><b>system[last block]</b> — caches the system prompt on top of tools.</description></item>
    ///   <item><description><b>messages[2nd-last assistant]</b> — <i>read</i> breakpoint: matches the cache the previous turn wrote at this exact prefix.</description></item>
    ///   <item><description><b>messages[last assistant]</b> — <i>write</i> breakpoint: caches the latest extended prefix for the next turn to read.</description></item>
    /// </list>
    /// <para>
    /// Per turn the API processes only the delta since the last assistant
    /// (typically a single <c>tool_result</c> + the new assistant response),
    /// while the entire conversation up to that point is read from cache.
    /// </para>
    /// <para>
    /// Anthropic silently ignores breakpoints under the per-model token minimum
    /// (Sonnet 4.5 / Opus 4.x = 1 024; Sonnet 4.6 = 2 048; Haiku 4.5 / Opus 4.5+ = 4 096),
    /// so it is safe to apply them liberally even when the prefix is short.
    /// Cache TTL is 5 min by default or 1 h via <see cref="Cache.OneHour"/>.
    /// </para>
    /// </summary>
    internal static void ApplyCaching(ILlm llm, AnthropicMessagesRequest request)
    {
        if (llm is not AnthropicBase anth) return;

        // Strip stale markers first. The agent session reuses the same
        // _messages list across turns, so cache_control set on an assistant
        // block in turn N would still be there in turn N+1; layering 2 fresh
        // markers on top would push the request over Anthropic's hard limit
        // of 4 cache_control blocks per request ("Found 5" — invalid request).
        ClearCacheMarkers(request);

        if (anth.Cache == Cache.None) return;

        var ttl = anth.Cache == Cache.OneHour ? "1h" : null;
        AnthropicCacheControl Marker() => new() { Ttl = ttl };

        // BP1: tools.
        if (request.Tools is { Count: > 0 } tools)
            tools[^1].CacheControl = Marker();

        // BP2: system prompt.
        if (request.System is { Count: > 0 } sys)
            sys[^1].CacheControl = Marker();

        // BP3 + BP4: rolling assistant cache.
        //   Older marker = read hit (matches what the previous turn wrote).
        //   Newer marker = write target (read by the next turn).
        // On turn 1 there is no assistant yet — both skipped. On turn 2 only
        // one assistant exists — mark it as the write target only.
        // Per Anthropic's lookback rules a single message-level breakpoint
        // would suffice for short conversations (the API walks back 20 blocks
        // searching for prior writes), but parallel tool calls easily push
        // the gap past that window — the second marker guarantees a hit.
        var marked = 0;
        for (var i = request.Messages.Count - 1; i >= 0 && marked < 2; i--)
        {
            var msg = request.Messages[i];
            if (msg.Role != "assistant") continue;

            var target = PickCacheTarget(msg.Content);
            if (target is null) continue;

            target.CacheControl = Marker();
            marked++;
        }
    }

    /// <summary>
    /// Returns the last cache-eligible content block in <paramref name="content"/>,
    /// or <c>null</c> if the message has nothing markable. Per Anthropic's
    /// caching spec:
    /// <list type="bullet">
    ///   <item><description><c>thinking</c> blocks cannot carry <c>cache_control</c> directly (they are still cached implicitly when a later block in the same prefix is marked).</description></item>
    ///   <item><description>Empty <c>text</c> blocks cannot be cached.</description></item>
    /// </list>
    /// </summary>
    private static AnthropicContentBlock? PickCacheTarget(List<AnthropicContentBlock> content)
    {
        for (var i = content.Count - 1; i >= 0; i--)
        {
            var block = content[i];
            if (block.Type == "thinking") continue;
            if (block.Type == "text" && string.IsNullOrEmpty(block.Text)) continue;
            return block;
        }
        return null;
    }

    /// <summary>
    /// Removes any <c>cache_control</c> already attached to tools, system or
    /// message content blocks. Required because the agent loop mutates a
    /// shared message list across turns and we re-derive breakpoints fresh on
    /// every call.
    /// </summary>
    private static void ClearCacheMarkers(AnthropicMessagesRequest request)
    {
        if (request.Tools is { } tools)
            foreach (var t in tools) t.CacheControl = null;

        if (request.System is { } sys)
            foreach (var s in sys) s.CacheControl = null;

        foreach (var msg in request.Messages)
            foreach (var c in msg.Content) c.CacheControl = null;
    }

    /// <summary>
    /// Maps <see cref="ReasoningEffort"/> to the wire-level
    /// <c>output_config.effort</c> string accepted by adaptive-thinking models.
    /// </summary>
    private static string EffortToWire(ReasoningEffort effort) => effort switch
    {
        ReasoningEffort.Low => "low",
        ReasoningEffort.Medium => "medium",
        ReasoningEffort.High => "high",
        // C# name is Extra; Anthropic's current API still expects "xhigh".
        // When the API switches to "extra", change only this line.
        ReasoningEffort.Extra => "xhigh",
        ReasoningEffort.Max => "max",
        _ => "high",
    };

    /// <summary>
    /// Builds the exception message for a failed Anthropic API call. For the
    /// "prompt is too long" 400 — historically the most mis-diagnosed failure,
    /// because the server reports a bare "&gt; 200000 maximum" with no model
    /// name — it names the model and its context window and points at the fix,
    /// so a 200k-vs-1M model mismatch is obvious from the exception alone.
    /// </summary>
    internal static string BuildApiErrorMessage(ILlm llm, System.Net.HttpStatusCode status, string body)
    {
        var message = $"Anthropic API failed for model '{llm.Name}' (context window {llm.MaxInputTokens} input tokens): {status}: {body}";

        if ((int)status == 400 &&
            body.Contains("prompt is too long", StringComparison.OrdinalIgnoreCase))
        {
            message += $" - the prompt exceeded model '{llm.Name}'s {llm.MaxInputTokens}-token context window. "
                     + "Route this step to a 1M-context model (Opus46/Opus47/Opus48 or Sonnet46) or reduce the input.";
        }

        return message;
    }

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var request = BuildRequest(llm, prompt, typeof(TResponse));
        var jsonPayload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);

        _logger.LogDebug("Anthropic request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException(BuildApiErrorMessage(llm, response.StatusCode, responseJson));
        }

        var anthropicResponse = JsonSerializer.Deserialize(responseJson, AnthropicJsonContext.Default.AnthropicResponse)!;

        var payload = ExtractPayloadOrThrow(anthropicResponse, typeof(TResponse), "GenerateAsync", llm);

        var result = ParseResponse<TResponse>(payload);

        var cachedReadTokens = anthropicResponse.Usage?.CacheReadInputTokens ?? 0;
        var cacheWriteTokens = anthropicResponse.Usage?.CacheCreationInputTokens ?? 0;
        // Anthropic reports input_tokens EXCLUSIVE of the cache buckets (the three
        // counts are disjoint). AiCostCalculator — matching the OpenAI convention —
        // treats InputTokens as the grand total and subtracts the cache buckets back
        // out, so normalize to the inclusive total here. Without this the uncached
        // input collapses to 0 whenever caching is active and cost is under-reported.
        var inputTokens = (anthropicResponse.Usage?.InputTokens ?? 0) + cachedReadTokens + cacheWriteTokens;
        var outputTokens = anthropicResponse.Usage?.OutputTokens ?? 0;

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cachedReadTokens,
            CacheWriteTokens = cacheWriteTokens
        });

        return new Result<TResponse>
        {
            Value = result,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = PromptNameResolver.Resolve(prompt),
                Duration = stopwatch.Elapsed,
                RequestId = anthropicResponse.Id,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    CachedTokens = cachedReadTokens,
                    CacheWriteTokens = cacheWriteTokens,
                    InputCost = inputCost,
                    OutputCost = outputCost
                }
            }
        };
    }

    /// <summary>
    /// Builds an actionable exception when a non-streaming Anthropic response
    /// arrives without a text block. The terminal <c>stop_reason</c> tells
    /// the caller exactly what to do next: bump <c>MaxTokens</c>, switch to
    /// the agent path for server-tool continuations, or surface the model's
    /// refusal. Replaces the previous cryptic "No text in Anthropic response"
    /// message that left users guessing why the call failed.
    /// </summary>
    private InvalidOperationException BuildEmptyResponseError(string operation, AnthropicResponse response, ILlm llm)
    {
        var stop = response.StopReason ?? "(unknown)";
        var msg = stop switch
        {
            "max_tokens" =>
                $"Anthropic {operation} returned no text: stop_reason=max_tokens. "
                + $"The response was truncated — likely the entire budget was spent on extended thinking. "
                + $"Increase {nameof(ILlm.MaxOutputTokens)} on '{llm.Name}' or lower the thinking effort.",
            "pause_turn" =>
                $"Anthropic {operation} returned stop_reason=pause_turn. "
                + "This indicates Anthropic's server-side sampling loop hit its iteration limit while running "
                + "server tools (web_search / web_fetch / code execution). Single-shot calls do not auto-resume — "
                + "use the agent path (IAiProvider.GenerateAsync overload taking IAgentLlm) which transparently "
                + "handles pause_turn continuations.",
            "refusal" =>
                $"Anthropic {operation} returned stop_reason=refusal — the model declined to continue.",
            _ =>
                $"Anthropic {operation} returned no text (stop_reason={stop}, request_id={response.Id ?? "(none)"})."
        };

        if (string.Equals(stop, "max_tokens", StringComparison.Ordinal) ||
            string.Equals(stop, "refusal", StringComparison.Ordinal) ||
            string.Equals(stop, "pause_turn", StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Anthropic {Operation} produced empty text content (stop_reason={StopReason}, request_id={RequestId}).",
                operation, stop, response.Id);
        }

        return new InvalidOperationException(msg);
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
        var jsonPayload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);

        _logger.LogDebug("Anthropic chat request: {Payload}", jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic chat error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException(BuildApiErrorMessage(llm, response.StatusCode, responseJson));
        }

        var anthropicResponse = JsonSerializer.Deserialize(responseJson, AnthropicJsonContext.Default.AnthropicResponse)!;

        var payload = ExtractPayloadOrThrow(anthropicResponse, typeof(TResponse), "ChatAsync", llm);

        var result = ParseResponse<TResponse>(payload);

        var cachedReadTokens = anthropicResponse.Usage?.CacheReadInputTokens ?? 0;
        var cacheWriteTokens = anthropicResponse.Usage?.CacheCreationInputTokens ?? 0;
        // Anthropic reports input_tokens EXCLUSIVE of the cache buckets (the three
        // counts are disjoint). AiCostCalculator — matching the OpenAI convention —
        // treats InputTokens as the grand total and subtracts the cache buckets back
        // out, so normalize to the inclusive total here. Without this the uncached
        // input collapses to 0 whenever caching is active and cost is under-reported.
        var inputTokens = (anthropicResponse.Usage?.InputTokens ?? 0) + cachedReadTokens + cacheWriteTokens;
        var outputTokens = anthropicResponse.Usage?.OutputTokens ?? 0;

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cachedReadTokens,
            CacheWriteTokens = cacheWriteTokens
        });

        return new Result<TResponse>
        {
            Value = result,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = PromptNameResolver.Resolve(prompt),
                Duration = stopwatch.Elapsed,
                RequestId = anthropicResponse.Id,
                Usage = new TokenUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    CachedTokens = cachedReadTokens,
                    CacheWriteTokens = cacheWriteTokens,
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
    public async IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(llm, prompt, typeof(TResponse));
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);

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
            var chunk = JsonSerializer.Deserialize(data, AnthropicJsonContext.Default.StreamEvent);

            if (chunk?.Type == "content_block_delta" && chunk.Delta?.Text != null)
                yield return chunk.Delta.Text;
            else if (chunk?.Type == "message_delta" && chunk.Delta?.StopReason is { } sr)
                LogTerminalStreamStopReason("StreamAsync", sr);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(
        ILlm llm,
        IPrompt prompt,
        IReadOnlyList<ChatMessage> chat,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Reuse BuildChatRequest for free-form (string) output and flip on streaming.
        // We adapt the non-generic IPrompt to IPrompt<string> via the shim helper.
        var request = BuildChatRequest<string>(llm, new ChatFallback.PromptShim(prompt), chat, typeof(string));
        request.Stream = true;

        var jsonPayload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);
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
            var chunk = JsonSerializer.Deserialize(data, AnthropicJsonContext.Default.StreamEvent);

            if (chunk?.Type == "content_block_delta" && chunk.Delta?.Text != null)
                yield return chunk.Delta.Text;
            else if (chunk?.Type == "message_delta" && chunk.Delta?.StopReason is { } sr)
                LogTerminalStreamStopReason("ChatStreamAsync", sr);
        }
    }

    /// <summary>
    /// Diagnostic-only logging for non-agent streaming paths. We do not
    /// auto-retry single-shot streams on <c>pause_turn</c> (that belongs to
    /// the agent loop, which buffers full assistant content and re-issues
    /// transparently) but we still want a clear log line so a silently
    /// truncated stream is never invisible to the operator.
    /// </summary>
    private void LogTerminalStreamStopReason(string operation, string stopReason)
    {
        switch (stopReason)
        {
            case "end_turn":
            case "stop_sequence":
                // Normal terminations — no log needed.
                break;
            case "max_tokens":
                _logger.LogWarning(
                    "Anthropic {Operation} stream terminated with stop_reason=max_tokens — the response was truncated. " +
                    "Increase MaxOutputTokens on the model or lower thinking effort.",
                    operation);
                break;
            case "pause_turn":
                _logger.LogWarning(
                    "Anthropic {Operation} stream terminated with stop_reason=pause_turn — the server-side sampling " +
                    "loop hit its iteration limit (server tools). Single-shot streams do not auto-resume; switch to " +
                    "the agent path (IAgentLlm) for transparent pause_turn continuations.",
                    operation);
                break;
            case "refusal":
                _logger.LogWarning(
                    "Anthropic {Operation} stream terminated with stop_reason=refusal — the model declined to continue.",
                    operation);
                break;
            default:
                _logger.LogDebug(
                    "Anthropic {Operation} stream terminated with stop_reason={StopReason}.",
                    operation, stopReason);
                break;
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
        // Always opt-in to two betas (comma-separated; both no-ops when unused,
        // so it is safe to enable globally and avoids per-request header juggling):
        //   • extended-cache-ttl-2025-04-11 — enables cache_control ttl="1h".
        //   • context-1m-2025-08-07 — unlocks the 1M-token context window on
        //     models that support it (Opus / Sonnet 4.6+). Redundant where 1M is
        //     already GA on the first-party API, but required on routes that still
        //     gate it behind the flag; harmless on 200k-only models.
        //   • fast-mode-2026-02-01 — lets a request set speed:"fast" (see IFast).
        //     Inert without the speed field, so safe to send globally — Anthropic's
        //     own fallback example sends this header with standard speed too.
        _httpClient.DefaultRequestHeaders.Add("anthropic-beta", "extended-cache-ttl-2025-04-11,context-1m-2025-08-07,fast-mode-2026-02-01");

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        }
    }

    /// <summary>Name of the synthetic tool used to force schema-valid structured output.</summary>
    private const string StructuredToolName = "respond_json";

    /// <summary>
    /// Routes structured output (<paramref name="responseType"/> != <c>string</c>)
    /// through a tool call instead of free-text JSON. Anthropic constrains a
    /// tool's <c>input</c> to its <c>input_schema</c> and always emits
    /// well-formed JSON — this eliminates the malformed-JSON failures (e.g. an
    /// unescaped <c>"</c> inside a translated phrase) that plague the
    /// "reply with raw JSON" technique on models without a native JSON mode.
    /// <para>
    /// With extended thinking on, Anthropic forbids forcing <c>tool_choice</c>,
    /// so we use <c>auto</c> and rely on the instruction (plus the free-text
    /// parser as a last-resort fallback). With thinking off we force the tool,
    /// which makes valid structured JSON guaranteed.
    /// </para>
    /// </summary>
    private static void ApplyStructuredOutputTool(
        AnthropicMessagesRequest request,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        if (responseType == typeof(string)) return;

        (request.Tools ??= new List<AnthropicTool>()).Add(new AnthropicTool
        {
            Name = StructuredToolName,
            Description = "Return the final answer as structured data. Call this tool exactly once with arguments that match its input schema.",
            InputSchema = AiSchemaRegistry.GetSchema(responseType),
        });

        request.ToolChoice = request.Thinking is null
            ? new AnthropicToolChoice { Type = "tool", Name = StructuredToolName }
            : new AnthropicToolChoice { Type = "auto" };
    }

    /// <summary>
    /// Returns the JSON payload to parse for a response: the structured tool
    /// call's <c>input</c> when present (the robust path), otherwise the first
    /// text block (free-text fallback for plain-string responses and for the
    /// thinking + <c>auto</c> case where the model may answer in prose).
    /// Throws <see cref="BuildEmptyResponseError"/> when neither is available.
    /// </summary>
    private string ExtractPayloadOrThrow(
        AnthropicResponse response,
        Type responseType,
        string operation,
        ILlm llm)
    {
        if (responseType != typeof(string))
        {
            var toolInput = response.Content?
                .FirstOrDefault(c => c.Type == "tool_use" && c.Name == StructuredToolName)?
                .Input;
            if (toolInput is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } el)
                return el.GetRawText();
        }

        var text = response.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
        if (string.IsNullOrEmpty(text))
            throw BuildEmptyResponseError(operation, response, llm);
        return text;
    }

    private static AnthropicMessagesRequest BuildRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var request = new AnthropicMessagesRequest
        {
            Model = llm.Name,
            MaxTokens = llm.MaxTokens,
        };

        var requiredMinTokens = ApplyThinking(llm, request);
        if (request.MaxTokens < requiredMinTokens)
            request.MaxTokens = requiredMinTokens;

        if (llm is IFast { Speed: SpeedType.Fast })
            request.Speed = "fast";

        var systemPrompt = string.Empty;
        if (responseType != typeof(string))
        {
            // Structured output is enforced via a forced tool call (see
            // ApplyStructuredOutputTool); the schema travels in the tool's
            // input_schema, so the prompt only needs to point the model at it.
            systemPrompt = "When you have the final answer, return it by calling the `respond_json` "
                + "tool with arguments matching its input schema. Do not also write the answer as plain text.";
        }

        if (!string.IsNullOrEmpty(systemPrompt))
            request.System = new List<AnthropicContentBlock> { new() { Type = "text", Text = systemPrompt } };

        var userText = prompt.Text;

        var content = new List<AnthropicContentBlock>
        {
            new() { Type = "text", Text = userText }
        };

        if (prompt.Files != null)
        {
            foreach (var file in prompt.Files.Where(f => f.IsImage))
            {
                content.Insert(0, new AnthropicContentBlock
                {
                    Type = "image",
                    Source = new AnthropicSource { Type = "base64", MediaType = file.MediaType.Value, Data = file.Base64 }
                });
            }

            foreach (var file in prompt.Files.Where(f => f.IsDocument))
            {
                if (file.MediaType == Asset.MimeType.ApplicationPdf)
                {
                    content.Insert(0, new AnthropicContentBlock
                    {
                        Type = "document",
                        Source = new AnthropicSource { Type = "base64", MediaType = file.MediaType.Value, Data = file.Base64 }
                    });
                }
            }
        }

        request.Messages.Add(new AnthropicMessageItem { Role = "user", Content = content });
        // No assistant "{" prefill (Sonnet 4.6+ rejects it; illegal with
        // extended thinking). Structured output is enforced by a forced tool
        // call instead — see ApplyStructuredOutputTool below.

        if (llm is AnthropicBase anthropicLlm)
        {
            if (anthropicLlm.TopP < 1.0)
                request.TopP = anthropicLlm.TopP;
            else if (anthropicLlm.Temperature < 1.0)
                request.Temperature = anthropicLlm.Temperature;
        }

        if (llm is AnthropicBase ab && ab.Tools is { Length: > 0 } typedTools)
        {
            request.Tools = BuildToolsForRequest(llm, typedTools);
        }

        ApplyStructuredOutputTool(request, responseType);

        ApplyCaching(llm, request);
        return request;
    }

    /// <summary>
    /// Translates <see cref="AnthropicBase.Tools"/> entries into Anthropic
    /// tool descriptors. <see cref="Tools.FunctionTool"/> becomes a function
    /// block; <see cref="Tools.WebSearchTool"/> becomes the
    /// <c>web_search_20250305</c> server tool with native <c>max_uses</c>,
    /// <c>allowed_domains</c>, <c>blocked_domains</c> and approximate
    /// <c>user_location</c> fields. Each case validates against
    /// <see cref="ILlm.SupportedTools"/>; a Haiku without WebSearch in its
    /// mask fails the build with a clear message instead of a 400 from the
    /// API.
    /// </summary>
    internal static List<AnthropicTool> BuildToolsForRequest(ILlm llm, IReadOnlyList<Tools.IAnthropicTool> tools)
    {
        var result = new List<AnthropicTool>(tools.Count);
        foreach (var t in tools)
            result.Add(BuildTool(llm, t));
        return result;
    }

    private static AnthropicTool BuildTool(ILlm llm, Tools.IAnthropicTool tool) => tool switch
    {
        Tools.FunctionTool f => new AnthropicTool
        {
            Name = f.Name,
            Description = f.Description,
            InputSchema = f.Parameters,
        },
        Tools.WebSearchTool w => RequireFlag(llm, ToolsType.WebSearch, w) is var _
            ? new AnthropicTool
            {
                Type = "web_search_20250305",
                Name = "web_search",
                MaxUses = w.MaxUses,
                AllowedDomains = w.AllowedDomains is null ? null : new List<string>(w.AllowedDomains),
                BlockedDomains = w.BlockedDomains is null ? null : new List<string>(w.BlockedDomains),
                UserLocation = HasLocation(w)
                    ? new AnthropicUserLocation
                    {
                        City = w.City,
                        Region = w.Region,
                        Country = w.Country,
                        Timezone = w.TimeZone,
                    }
                    : null,
            }
            : throw new InvalidOperationException("unreachable"),
        _ => throw new NotSupportedException(
            $"Anthropic provider does not yet wire tool '{tool.GetType().FullName}'. "
            + "Supported entries: FunctionTool, WebSearchTool. Code execution "
            + "(code_execution_20250522) and computer use require additional "
            + "beta headers and request shapes that this SDK has not modelled."),
    };

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

    private static bool HasLocation(Tools.WebSearchTool w) =>
        !string.IsNullOrEmpty(w.City)
        || !string.IsNullOrEmpty(w.Region)
        || !string.IsNullOrEmpty(w.Country)
        || !string.IsNullOrEmpty(w.TimeZone);

    private static AnthropicMessagesRequest BuildChatRequest<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type responseType)
    {
        var request = new AnthropicMessagesRequest
        {
            Model = llm.Name,
            MaxTokens = llm.MaxTokens,
        };

        var requiredMinTokens = ApplyThinking(llm, request);
        if (request.MaxTokens < requiredMinTokens)
            request.MaxTokens = requiredMinTokens;

        if (llm is IFast { Speed: SpeedType.Fast })
            request.Speed = "fast";

        // System message: in chat mode the rendered Prompt.Text IS the system
        // instruction (semantic flip vs single-shot GenerateAsync where Text =
        // user message). For structured output append the JSON schema clause.
        var systemPrompt = prompt.Text ?? string.Empty;
        // Retained because the message-assembly loop below references it; left
        // null because structured output now travels through a forced tool call,
        // not a free-text "reply with JSON" reminder.
        string? userJsonReminder = null;
        if (responseType != typeof(string))
        {
            // Structured output is enforced via a forced tool call (see
            // ApplyStructuredOutputTool); the schema travels in the tool's
            // input_schema. Append only a short pointer to the system message.
            var toolInstruction = "When you have the final answer, return it by calling the `respond_json` "
                + "tool with arguments matching its input schema. Do not also write the answer as plain text.";
            systemPrompt = string.IsNullOrEmpty(systemPrompt) ? toolInstruction : systemPrompt + "\n\n" + toolInstruction;
        }

        if (!string.IsNullOrEmpty(systemPrompt))
            request.System = new List<AnthropicContentBlock> { new() { Type = "text", Text = systemPrompt } };

        var messages = new List<AnthropicMessageItem>();
        var idx = 0;
        while (idx < chat.Count && chat[idx] is Assistant) idx++;

        var sessionFilesAttached = false;

        for (; idx < chat.Count; idx++)
        {
            switch (chat[idx])
            {
                case User u:
                {
                    var userContent = new List<AnthropicContentBlock>();
                    if (!sessionFilesAttached && prompt.Files != null)
                    {
                        AppendFiles(userContent, prompt.Files);
                        sessionFilesAttached = true;
                    }
                    if (u.Files != null) AppendFiles(userContent, u.Files);
                    userContent.Add(new AnthropicContentBlock { Type = "text", Text = u.Text });
                    messages.Add(new AnthropicMessageItem { Role = "user", Content = userContent });
                    break;
                }
                case Assistant a:
                    messages.Add(new AnthropicMessageItem
                    {
                        Role = "assistant",
                        Content = new List<AnthropicContentBlock> { new() { Type = "text", Text = a.Text } }
                    });
                    break;
                case Tool t:
                    messages.Add(new AnthropicMessageItem
                    {
                        Role = "user",
                        Content = new List<AnthropicContentBlock>
                        {
                            new()
                            {
                                Type = "tool_result",
                                ToolUseId = t.ToolCallId,
                                Content = t.ResultJson,
                                IsError = t.IsError
                            }
                        }
                    });
                    break;
            }
        }

        if (messages.Count == 0 || messages[^1].Role == "assistant")
        {
            var fallbackText = userJsonReminder?.TrimStart() ?? "Continue.";
            messages.Add(new AnthropicMessageItem
            {
                Role = "user",
                Content = new List<AnthropicContentBlock> { new() { Type = "text", Text = fallbackText } }
            });
            userJsonReminder = null;
        }
        else if (!string.IsNullOrEmpty(userJsonReminder))
        {
            var last = messages[^1];
            var firstText = last.Content.FirstOrDefault(b => b.Type == "text");
            if (firstText != null)
                firstText.Text = (firstText.Text ?? string.Empty) + userJsonReminder;
            else
                last.Content.Add(new AnthropicContentBlock { Type = "text", Text = userJsonReminder });
        }

        // No assistant "{" prefill (Sonnet 4.6+ rejects it; illegal with
        // extended thinking). Structured output is enforced by a forced tool
        // call instead — see ApplyStructuredOutputTool below.

        request.Messages = messages;

        if (llm is AnthropicBase anthropicLlm)
        {
            if (anthropicLlm.TopP < 1.0) request.TopP = anthropicLlm.TopP;
            else if (anthropicLlm.Temperature < 1.0) request.Temperature = anthropicLlm.Temperature;
        }

        if (llm is AnthropicBase ab && ab.Tools is { Length: > 0 } typedTools)
        {
            request.Tools = BuildToolsForRequest(llm, typedTools);
        }

        ApplyStructuredOutputTool(request, responseType);

        ApplyCaching(llm, request);
        return request;
    }

    private static void AppendFiles(List<AnthropicContentBlock> content, IReadOnlyList<Asset> files)
    {
        foreach (var file in files.Where(f => f.IsImage))
        {
            content.Add(new AnthropicContentBlock
            {
                Type = "image",
                Source = new AnthropicSource { Type = "base64", MediaType = file.MediaType.Value, Data = file.Base64 }
            });
        }
        foreach (var file in files.Where(f => f.IsDocument && f.MediaType == Asset.MimeType.ApplicationPdf))
        {
            content.Add(new AnthropicContentBlock
            {
                Type = "document",
                Source = new AnthropicSource { Type = "base64", MediaType = file.MediaType.Value, Data = file.Base64 }
            });
        }
    }

    private static TResponse ParseResponse<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(string json)
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
            // AOT-safe deserialize via the source-generated JsonTypeInfo<TResponse>
            // (also unwraps an optional {"result":…} envelope internally).
            return JsonResponseParser.DeserializeStructured<TResponse>(jsonContent);
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
    /// <summary>
    /// Why the model stopped: <c>end_turn</c> (normal completion),
    /// <c>max_tokens</c> (truncated), <c>tool_use</c> (model wants to call a
    /// client tool), <c>pause_turn</c> (server-side iteration limit reached
    /// for server tools — caller must re-issue with the same content array),
    /// <c>refusal</c> (model declined). Required for sensible diagnostics on
    /// the non-streaming path: a missing <c>text</c> block alone tells the
    /// caller nothing about whether the request needs more tokens, a retry,
    /// or has been refused.
    /// </summary>
    public string? StopReason { get; set; }
}

internal sealed class AnthropicContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    // tool_use blocks: the model's structured answer arrives as the tool's
    // `input` — Anthropic constrains it to the tool's input_schema and emits
    // well-formed JSON, so it parses safely even when free text would not.
    public string? Name { get; set; }
    public JsonElement? Input { get; set; }
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
    /// <summary>
    /// Terminal stop reason on the <c>message_delta</c> event. Surfaced for
    /// diagnostics in single-shot streaming paths so a silently-truncated
    /// response (max_tokens / pause_turn / refusal) is logged instead of
    /// just ending the IAsyncEnumerable.
    /// </summary>
    public string? StopReason { get; set; }
}

// Request models (AOT-safe DTO).
internal sealed class AnthropicMessagesRequest
{
    public string Model { get; set; } = "";
    public int MaxTokens { get; set; }
    /// <summary>Inference speed: <c>"fast"</c> opts into fast mode (requires the <c>fast-mode-2026-02-01</c> beta header). Null/omitted = standard speed.</summary>
    public string? Speed { get; set; }
    /// <summary>System prompt as content blocks. Array form is required to attach <c>cache_control</c>; Anthropic accepts it identically to the string form for non-cached requests.</summary>
    public List<AnthropicContentBlock>? System { get; set; }
    public List<AnthropicMessageItem> Messages { get; set; } = new();
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public bool? Stream { get; set; }
    public List<AnthropicTool>? Tools { get; set; }
    public AnthropicToolChoice? ToolChoice { get; set; }
    public AnthropicThinking? Thinking { get; set; }
    public AnthropicOutputConfig? OutputConfig { get; set; }
}

/// <summary>
/// Controls tool selection. <c>type</c>: <c>auto</c> (model decides — the only
/// value Anthropic allows while extended thinking is enabled), <c>tool</c>
/// (force the tool named by <see cref="Name"/>), <c>any</c>, or <c>none</c>.
/// </summary>
internal sealed class AnthropicToolChoice
{
    public string Type { get; set; } = "";
    public string? Name { get; set; }
}

internal sealed class AnthropicOutputConfig
{
    /// <summary>Adaptive-thinking effort hint: <c>low|medium|high|xhigh|max</c>.</summary>
    public string? Effort { get; set; }
}

internal sealed class AnthropicMessageItem
{
    public string Role { get; set; } = "";
    public List<AnthropicContentBlock> Content { get; set; } = new();
}

internal sealed class AnthropicContentBlock
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public AnthropicSource? Source { get; set; }
    public string? ToolUseId { get; set; }
    public string? Content { get; set; }
    public bool? IsError { get; set; }
    // tool_use blocks emitted by the assistant in agent sessions.
    public string? Id { get; set; }
    public string? Name { get; set; }
    public JsonElement? Input { get; set; }
    // thinking blocks (extended thinking).
    public string? Thinking { get; set; }
    public string? Signature { get; set; }
    /// <summary>
    /// Encrypted opaque payload for <c>redacted_thinking</c> blocks. Anthropic
    /// returns these when portions of the model's reasoning are safety-redacted
    /// (separate from the regular <c>thinking</c> stream). Per Anthropic docs
    /// the entire block — including this field — must be round-tripped
    /// unchanged on follow-up turns: filtering on
    /// <c>block.Type == "thinking"</c> alone silently drops them and breaks
    /// the multi-turn extended-thinking protocol, surfacing as agent turns
    /// that simply stop responding mid-task.
    /// </summary>
    public string? Data { get; set; }
    /// <summary>Optional cache breakpoint marking this block as the end of a cacheable prefix.</summary>
    public AnthropicCacheControl? CacheControl { get; set; }
}

internal sealed class AnthropicSource
{
    public string Type { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string Data { get; set; } = "";
}

internal sealed class AnthropicTool
{
    /// <summary>
    /// Optional discriminator for server-side tools (e.g. <c>web_search_20250305</c>,
    /// <c>code_execution_20250522</c>, <c>computer_20250124</c>). Function tools
    /// leave this null — Anthropic infers them by the presence of <c>input_schema</c>.
    /// </summary>
    public string? Type { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>
    /// JSON Schema describing the function arguments. Required for function
    /// tools; left <c>null</c> for server tools (web_search, code_execution,
    /// computer use) where Anthropic's wire shape forbids <c>input_schema</c>.
    /// Must stay nullable: <see cref="JsonElement"/> is a struct, so a non-
    /// nullable property in <c>default</c> state has <c>ValueKind == Undefined</c>
    /// and the source-generated converter throws
    /// <see cref="InvalidOperationException"/> from <c>JsonElementConverter.Write</c>
    /// on an "uninitialised JsonElement" — even with
    /// <c>DefaultIgnoreCondition = WhenWritingNull</c>, because a struct is
    /// never null.
    /// </summary>
    public JsonElement? InputSchema { get; set; }
    /// <summary>Marks a cache breakpoint covering this tool and everything before it (system + earlier tools).</summary>
    public AnthropicCacheControl? CacheControl { get; set; }

    // ---- Server-tool parameters (web_search_20250305) ----

    /// <summary>Maximum search invocations allowed in a single request.</summary>
    public int? MaxUses { get; set; }
    /// <summary>Optional allow-list filter; results outside this list are dropped.</summary>
    public List<string>? AllowedDomains { get; set; }
    /// <summary>Optional block-list filter; results from these domains are dropped.</summary>
    public List<string>? BlockedDomains { get; set; }
    /// <summary>Approximate user location passed through to the search engine.</summary>
    public AnthropicUserLocation? UserLocation { get; set; }
}

/// <summary>Anthropic <c>user_location</c> hint for server-side web search.</summary>
internal sealed class AnthropicUserLocation
{
    public string Type { get; set; } = "approximate";
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public string? Timezone { get; set; }
}

internal sealed class AnthropicCacheControl
{
    public string Type { get; set; } = "ephemeral";
    /// <summary><c>"1h"</c> for the 1-hour beta cache; null/omitted for the default 5-minute TTL.</summary>
    public string? Ttl { get; set; }
}

internal sealed class AnthropicThinking
{
    /// <summary>Either <c>"enabled"</c> (legacy + <see cref="BudgetTokens"/>) or <c>"adaptive"</c> (effort hint via <c>output_config</c>).</summary>
    public string Type { get; set; } = "";
    public int? BudgetTokens { get; set; }
}

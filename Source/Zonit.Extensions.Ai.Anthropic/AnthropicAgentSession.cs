using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Stateful agent session for Anthropic Messages API. Client-side message
/// history is maintained across turns.
/// </summary>
/// <remarks>
/// AOT-safe: the request schema is sourced from the build-time
/// <c>AiSchemaRegistry</c> and every request/response payload goes through the
/// source-generated <c>AnthropicJsonContext</c> or <c>JsonDocument</c>. No
/// class-level trim/AOT suppression is required.
/// </remarks>
internal sealed class AnthropicAgentSession : IAgentSession
{
    private readonly HttpClient _httpClient;
    private readonly AgentSessionContext _context;
    private readonly AiResilienceOptions _resilience;
    private readonly ILogger _logger;

    // Accumulating conversation history.
    private readonly List<AnthropicMessageItem> _messages = new();
    private int _turnIndex;

    public AnthropicAgentSession(HttpClient httpClient, AgentSessionContext context, AiResilienceOptions resilience, ILogger logger)
    {
        _httpClient = httpClient;
        _context = context;
        _resilience = resilience;
        _logger = logger;
    }

    /// <summary>
    /// Hard cap on the number of consecutive <c>stop_reason=pause_turn</c>
    /// continuations issued <i>within a single agent turn</i>. Anthropic's
    /// server-side sampling loop pauses every ~10 iterations when running
    /// server tools (web_search, web_fetch, code execution) and we resume it
    /// transparently. The cap exists purely as a runaway-protection guard:
    /// in healthy usage we typically observe at most one or two
    /// continuations per agent turn.
    /// </summary>
    private const int MaxPauseContinuations = 10;

    // Matches the [RequiresUnreferencedCode]/[RequiresDynamicCode] on
    // IAgentSession.RunTurnAsync (mandatory per IL2046/IL3051 — annotations must
    // match across interface implementations). The honest cost is the agent
    // runner's reflective tail: it parses the final TResponse via the
    // JsonResponseParser reflection fallback and may execute reflection-based
    // tools (ToolBase / MCP). This session's own request build is AOT-safe
    // (build-time AiSchemaRegistry schema + source-generated AnthropicJsonContext).
    [RequiresUnreferencedCode("The agent runner parses the final response and may run reflection-based tools; request building here is AOT-safe.")]
    [RequiresDynamicCode("The agent runner parses the final response and may run reflection-based tools; request building here is AOT-safe.")]
    public async Task<AgentTurn> RunTurnAsync(
        IReadOnlyList<ToolResult>? toolResults,
        CancellationToken cancellationToken)
    {
        _turnIndex++;

        if (_turnIndex == 1)
        {
            var seeded = AppendInitialChatHistory();
            if (!seeded)
                AppendInitialUserMessage();
        }
        else
        {
            AppendToolResultsMessage(toolResults ?? Array.Empty<ToolResult>());
        }

        // Accumulators that span every Anthropic round-trip belonging to this
        // single agent turn. A turn becomes more than one round-trip when
        // Anthropic returns stop_reason=pause_turn — the server-side sampling
        // loop hit its built-in iteration limit (default 10 iterations for
        // server tools like web_search / web_fetch / code execution) and asks
        // the client to resend the conversation as-is so it can continue.
        // The agent runner sees only the consolidated turn; pause_turn is an
        // internal Anthropic protocol detail.
        var combinedToolCalls = new List<PendingToolCall>();
        var combinedFinalText = new StringBuilder();
        var combinedInput = 0;
        var combinedOutput = 0;
        var combinedCacheRead = 0;
        var combinedCacheWrite = 0;
        var combinedDuration = TimeSpan.Zero;
        string? firstRequestId = null;
        string? lastStopReason = null;
        var pauseContinuations = 0;

        while (true)
        {
            var request = BuildRequest();
            // Streaming is mandatory for the agent loop. Non-streaming responses on
            // long agent turns (80+ tools, extended thinking, growing context) leave
            // the HTTP/2 connection idle for minutes, which routers and proxies
            // silently drop — the client then waits the full AttemptTimeout for a
            // response that will never arrive. Anthropic's own docs require
            // streaming for any request that may take longer than ~10 minutes:
            // periodic `ping` events keep TCP alive and content deltas arrive
            // incrementally, eliminating the idle-connection failure mode.
            request.Stream = true;

            var payload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);
            if (pauseContinuations == 0)
                _logger.LogDebug("Anthropic agent turn {Turn} payload: {Payload}", _turnIndex, payload);
            else
                _logger.LogDebug("Anthropic agent turn {Turn} pause-continuation #{Pause} payload: {Payload}", _turnIndex, pauseContinuations, payload);

            var sw = Stopwatch.StartNew();
            var aggregated = await SendStreamingWithRetriesAsync(payload, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            // Append assistant content to history immediately so the next loop
            // iteration (pause_turn continuation) AND the next agent turn
            // (after tool execution) see the full assistant turn including
            // thinking blocks, redacted_thinking blocks, tool_use, and text.
            // Anthropic requires the entire unmodified content array on the
            // follow-up request — dropping any block (especially thinking /
            // redacted_thinking) breaks the multi-turn extended-thinking
            // protocol and surfaces as the model abruptly stopping mid-task.
            _messages.Add(new AnthropicMessageItem { Role = "assistant", Content = aggregated.AssistantContent });

            combinedToolCalls.AddRange(aggregated.ToolCalls);
            if (aggregated.FinalText.Length > 0)
                combinedFinalText.Append(aggregated.FinalText);
            combinedInput += aggregated.InputTokens;
            combinedOutput += aggregated.OutputTokens;
            combinedCacheRead += aggregated.CacheReadTokens;
            combinedCacheWrite += aggregated.CacheWriteTokens;
            combinedDuration += sw.Elapsed;
            firstRequestId ??= aggregated.RequestId;
            lastStopReason = aggregated.StopReason;

            if (!string.Equals(aggregated.StopReason, "pause_turn", StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "Anthropic agent turn {Turn} finished: stop_reason={StopReason}, tool_calls={ToolCalls}, text_len={TextLen}, in_tokens={In}, out_tokens={Out}",
                    _turnIndex, aggregated.StopReason ?? "(null)", aggregated.ToolCalls.Count, aggregated.FinalText.Length, aggregated.InputTokens, aggregated.OutputTokens);
                break;
            }

            // pause_turn: Anthropic capped its server-side sampling loop. Per
            // https://platform.claude.com/docs/en/build-with-claude/handling-stop-reasons#pause_turn
            // we MUST resend the conversation including the assistant message
            // unmodified to let Claude finish. We do NOT execute any tool
            // calls here — pause_turn is a server-tool internal-loop pause,
            // distinct from stop_reason=tool_use which is the client-tool
            // signal the agent runner consumes.
            pauseContinuations++;
            if (pauseContinuations >= MaxPauseContinuations)
            {
                _logger.LogWarning(
                    "Anthropic pause_turn continuation cap ({Max}) reached on agent turn {Turn}; surfacing partial response. " +
                    "This is unusual — a single turn rarely needs more than one or two server-side continuations.",
                    MaxPauseContinuations, _turnIndex);
                break;
            }
            _logger.LogDebug(
                "Anthropic returned stop_reason=pause_turn on turn {Turn} (continuation {Count}/{Max}); re-issuing transparently.",
                _turnIndex, pauseContinuations, MaxPauseContinuations);
        }

        return BuildTurn(
            combinedToolCalls,
            combinedFinalText,
            combinedInput,
            combinedOutput,
            combinedCacheRead,
            combinedCacheWrite,
            combinedDuration,
            firstRequestId,
            lastStopReason);
    }

    /// <summary>
    /// Wraps <see cref="SendStreamingAsync"/> with bounded client-side retries
    /// for the failure modes Polly's <c>HttpClient.SendAsync</c>-level
    /// resilience handler cannot see: SSE inter-event stalls, mid-stream
    /// connection drops, and "empty-turn" data loss (only <c>thinking</c>
    /// blocks, no <c>text</c> / <c>tool_use</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anthropic's streaming API on Sonnet 4.6 / Opus 4.7 with high reasoning
    /// effort regularly produces 7–8 minute silent stalls and "thinking
    /// completed but never emitted any text" responses
    /// (<c>anthropics/anthropic-sdk-typescript#867</c>,
    /// <c>anthropics/claude-code#51568</c>). Both are server-side failures
    /// that look indistinguishable from a healthy long stream until the
    /// stream completes (or watchdog fires). Resending the identical request
    /// is the only known recovery path.
    /// </para>
    /// <para>
    /// This retry is independent of and complementary to Polly's
    /// <c>StandardResilienceHandler</c> retries on <c>HttpClient.SendAsync</c>:
    /// Polly recovers from connection failures and 5xx <i>before</i> SSE
    /// streaming begins; this loop recovers from failures that happen
    /// <i>after</i> SSE streaming has started.
    /// </para>
    /// </remarks>
    private async Task<AggregatedTurn> SendStreamingWithRetriesAsync(string payload, CancellationToken cancellationToken)
    {
        // The client-side stream retry follows the SAME shared schedule as the
        // HTTP-layer Polly retries — AiOptions.Resilience (MaxRetryAttempts +
        // RetryDelay's exponential backoff capped at RetryMaxDelay) — so retry
        // behaviour is configured once and identical across every provider.
        var maxRetries = Math.Max(0, _resilience.MaxRetryAttempts);
        var attempt = 0;
        while (true)
        {
            try
            {
                var aggregated = await SendStreamingAsync(payload, cancellationToken).ConfigureAwait(false);

                if (IsTerminalEmptyTurn(aggregated))
                {
                    // Genuine server-side data loss (end_turn / a stream truncated with only
                    // thinking blocks) is transient — re-issue the identical request while the
                    // retry budget lasts. max_tokens / refusal are NOT retried (a resend just
                    // re-truncates / re-refuses) and fall straight through to the throw below.
                    if (IsRetryableEmptyTurn(aggregated) && attempt < maxRetries)
                    {
                        attempt++;
                        var delay = _resilience.RetryDelay(attempt);
                        _logger.LogWarning(
                            "Anthropic agent turn {Turn} returned an empty assistant turn (stop_reason={StopReason}, " +
                            "blocks={Blocks}, out_tokens={Out}). Server-side data loss — retrying in {Delay} " +
                            "(attempt {Attempt}/{Max}). See anthropics/anthropic-sdk-typescript#867.",
                            _turnIndex, aggregated.StopReason ?? "(null)",
                            DescribeBlockTypes(aggregated.AssistantContent),
                            aggregated.OutputTokens, delay, attempt, maxRetries);
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // No usable content and no (more) retries: fail loudly with a typed,
                    // coded exception instead of surfacing an empty Value the caller would
                    // have to guard against. The throw escapes this loop untouched — the
                    // transient-failure catch below filters on IsTransientStreamFailure,
                    // which AiEmptyResponseException is not.
                    throw BuildEmptyTurnException(aggregated, attempt + 1);
                }

                return aggregated;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // honour caller cancellation immediately
            }
            catch (Exception ex) when (IsTransientStreamFailure(ex) && attempt < maxRetries)
            {
                attempt++;
                var delay = _resilience.RetryDelay(attempt);
                _logger.LogWarning(ex,
                    "Anthropic agent turn {Turn} streaming failed mid-stream ({Type}: {Message}). " +
                    "Retrying with same message history in {Delay} (attempt {Attempt}/{Max}).",
                    _turnIndex, ex.GetType().Name, ex.Message, delay, attempt, maxRetries);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Identifies failures that happen <i>after</i> SSE streaming has started
    /// (so Polly's <c>HttpClient.SendAsync</c>-level retries can no longer
    /// help) but are transient enough to be worth re-issuing.
    /// </summary>
    private static bool IsTransientStreamFailure(Exception ex) => ex switch
    {
        TimeoutException => true,           // SSE inter-event watchdog fired
        HttpRequestException => true,        // connection dropped mid-stream
        IOException => true,                 // socket closed mid-read
        _ => false,
    };

    /// <summary>
    /// Detects a <i>terminal</i> empty assistant turn — the model emitted no
    /// actionable content (only <c>thinking</c> / <c>redacted_thinking</c>, or
    /// nothing) AND finished on a stop_reason the agent loop cannot continue
    /// (<c>end_turn</c> / <c>max_tokens</c> / <c>refusal</c>, or a stream that
    /// truncated before any <c>message_delta</c> set stop_reason).
    /// <c>tool_use</c> (has content) and <c>pause_turn</c> (the loop resumes it)
    /// are intentionally excluded — both have legitimate continuations.
    /// </summary>
    private static bool IsTerminalEmptyTurn(AggregatedTurn agg)
    {
        if (HasObservableContent(agg.AssistantContent)) return false;

        return agg.StopReason switch
        {
            "end_turn" => true,
            "max_tokens" => true,
            "refusal" => true,
            null => true, // stream truncated before message_delta — treat as data loss
            _ => false,   // pause_turn / tool_use: not terminal-empty
        };
    }

    /// <summary>
    /// The retryable subset of <see cref="IsTerminalEmptyTurn"/>: only genuine
    /// server-side data loss (<c>end_turn</c> with no content, or a truncated
    /// stream) is worth re-issuing. <c>max_tokens</c> (re-truncates) and
    /// <c>refusal</c> (re-refuses) are deterministic given the same request, so
    /// resending them only burns tokens — they are surfaced immediately.
    /// </summary>
    private static bool IsRetryableEmptyTurn(AggregatedTurn agg)
        => agg.StopReason is "end_turn" or null && !HasObservableContent(agg.AssistantContent);

    /// <summary>
    /// Maps a terminal empty turn's stop_reason to a stable
    /// <see cref="AiResponseError"/> code and builds the typed exception. Called
    /// only when <see cref="IsTerminalEmptyTurn"/> already held, so the default
    /// arm (data loss) is the correct fallback for <c>end_turn</c> / <c>null</c>.
    /// </summary>
    private AiEmptyResponseException BuildEmptyTurnException(AggregatedTurn agg, int attempts)
    {
        var stop = agg.StopReason;
        var blocks = DescribeBlockTypes(agg.AssistantContent);

        var (code, message) = stop switch
        {
            "max_tokens" => (
                AiResponseError.Truncated,
                $"Anthropic agent turn {_turnIndex} on '{_context.Llm.Name}' returned no usable content: "
                + $"stop_reason=max_tokens (blocks={blocks}). The token budget was spent before any text/tool_use — "
                + $"raise MaxTokens or lower the reasoning effort."),
            "refusal" => (
                AiResponseError.Refusal,
                $"Anthropic agent turn {_turnIndex} on '{_context.Llm.Name}' was declined: stop_reason=refusal. "
                + "The model will not answer this input; revise the prompt / inputs."),
            _ => (
                AiResponseError.EmptyAfterRetries,
                $"Anthropic agent turn {_turnIndex} on '{_context.Llm.Name}' returned an empty assistant turn "
                + $"(stop_reason={stop ?? "(null)"}, blocks={blocks}) after {attempts} attempt(s). "
                + "Server-side data loss (anthropics/anthropic-sdk-typescript#867) — usually a transient Anthropic "
                + "incident; re-run the operation. Tune Ai:Resilience MaxRetryAttempts / RetryBaseDelay / RetryMaxDelay."),
        };

        _logger.LogError(
            "Anthropic agent turn {Turn} unusable empty turn → throwing AiEmptyResponseException "
            + "[AI-E{Code}] (stop_reason={StopReason}, blocks={Blocks}, attempts={Attempts}).",
            _turnIndex, (int)code, stop ?? "(null)", blocks, attempts);

        return new AiEmptyResponseException(code, message, stop, attempts);
    }

    /// <summary>
    /// True when the assistant content contains at least one block the agent
    /// runner / final-text pipeline can act on. <c>thinking</c> and
    /// <c>redacted_thinking</c> alone are NOT actionable.
    /// </summary>
    private static bool HasObservableContent(IReadOnlyList<AnthropicContentBlock> content)
    {
        for (int i = 0; i < content.Count; i++)
        {
            var t = content[i].Type;
            if (t is "text" or "tool_use") return true;
        }
        return false;
    }

    private static string DescribeBlockTypes(IReadOnlyList<AnthropicContentBlock> content)
    {
        if (content.Count == 0) return "(none)";
        var sb = new StringBuilder(content.Count * 8);
        for (int i = 0; i < content.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(content[i].Type ?? "?");
        }
        return sb.ToString();
    }

    private async Task<AggregatedTurn> SendStreamingAsync(string payload, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Anthropic agent error: {Status} - {Body}", response.StatusCode, errBody);
            throw new HttpRequestException(AnthropicProvider.BuildApiErrorMessage(_context.Llm, response.StatusCode, errBody));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var agg = new AggregatedTurn();
        var blocks = new SortedDictionary<int, BlockAccumulator>();

        // Inter-event SSE watchdog. After HttpCompletionOption.ResponseHeadersRead
        // the resilience handler is no longer in scope (SendAsync already
        // returned), so a server-side application freeze that still answers
        // HTTP/2 PING keep-alives at the transport layer would leave
        // ReadLineAsync blocked indefinitely. Anthropic <i>documents</i>
        // `ping` SSE events every ~10–15 s, but on Sonnet 4.6 / Opus 4.7
        // high-effort thinking the API has been observed to emit no frames
        // for several minutes — see AiResilienceOptions.InterEventTimeout
        // docs for community references. Going that timeout without ANY
        // frame is the dead-stream signal.
        //
        // The TimeoutException thrown below is caught and retried by
        // SendStreamingWithRetriesAsync — the SSE stall is the most common
        // Sonnet ReasonHigh failure mode and we never want a single stall to
        // kill an entire agent run.
        var interEventTimeout = _resilience.InterEventTimeout > TimeSpan.Zero
            ? _resilience.InterEventTimeout
            : TimeSpan.FromMinutes(30);
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        watchdog.CancelAfter(interEventTimeout);

        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(watchdog.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Anthropic agent stream stalled — no SSE event received for {Timeout}. Will be retried by SendStreamingWithRetriesAsync if retry budget remains.",
                    interEventTimeout);
                throw new TimeoutException(
                    $"Anthropic stream produced no event for {interEventTimeout.TotalSeconds:N0}s. "
                    + "Server-side stall (no ping frames). Configurable via Ai:Resilience InterEventTimeout / MaxRetryAttempts.");
            }

            if (line is null) break;

            // SSE frames are `event: <name>\ndata: <json>\n\n`. We rely on the
            // `type` field inside the JSON payload, so the `event:` lines and
            // blank separators are ignored. Refresh the watchdog on EVERY
            // physical line we successfully pulled from the wire — even
            // separators and `event:` headers count as "the server is alive".
            watchdog.CancelAfter(interEventTimeout);

            if (line.Length == 0 || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) continue;
            var eventType = typeEl.GetString();

            switch (eventType)
            {
                case "message_start":
                    HandleMessageStart(root, agg);
                    break;
                case "content_block_start":
                    HandleBlockStart(root, blocks);
                    break;
                case "content_block_delta":
                    HandleBlockDelta(root, blocks);
                    break;
                case "message_delta":
                    HandleMessageDelta(root, agg);
                    break;
                // Other event types (content_block_stop, message_stop, ping)
                // require no action — ping in particular is the keep-alive
                // signal that prevents the idle-drop scenario.
            }
        }

        FinalizeBlocks(blocks, agg);
        return agg;
    }

    private static void HandleMessageStart(JsonElement root, AggregatedTurn agg)
    {
        if (!root.TryGetProperty("message", out var msg)) return;
        if (msg.TryGetProperty("id", out var idEl)) agg.RequestId = idEl.GetString();
        if (!msg.TryGetProperty("usage", out var u)) return;
        if (u.TryGetProperty("input_tokens", out var it)) agg.InputTokens = it.GetInt32();
        if (u.TryGetProperty("output_tokens", out var ot)) agg.OutputTokens = ot.GetInt32();
        if (u.TryGetProperty("cache_read_input_tokens", out var cr)) agg.CacheReadTokens = cr.GetInt32();
        if (u.TryGetProperty("cache_creation_input_tokens", out var cw)) agg.CacheWriteTokens = cw.GetInt32();
    }

    private static void HandleBlockStart(JsonElement root, SortedDictionary<int, BlockAccumulator> blocks)
    {
        if (!root.TryGetProperty("index", out var idxEl)) return;
        if (!root.TryGetProperty("content_block", out var cbEl)) return;
        var idx = idxEl.GetInt32();
        var type = cbEl.GetProperty("type").GetString() ?? "";

        var acc = new BlockAccumulator { Type = type };
        switch (type)
        {
            case "text":
                if (cbEl.TryGetProperty("text", out var t)) acc.Text.Append(t.GetString());
                break;
            case "tool_use":
                acc.ToolId = cbEl.GetProperty("id").GetString();
                acc.ToolName = cbEl.GetProperty("name").GetString();
                break;
            case "thinking":
                if (cbEl.TryGetProperty("thinking", out var th)) acc.Text.Append(th.GetString());
                break;
            case "redacted_thinking":
                // Anthropic occasionally returns blocks where the reasoning
                // is safety-redacted; the encrypted payload arrives in `data`
                // (no thinking_delta / signature_delta follow). The block
                // MUST be round-tripped unchanged on subsequent turns.
                if (cbEl.TryGetProperty("data", out var rd)) acc.RedactedData = rd.GetString();
                break;
        }
        blocks[idx] = acc;
    }

    private static void HandleBlockDelta(JsonElement root, SortedDictionary<int, BlockAccumulator> blocks)
    {
        if (!root.TryGetProperty("index", out var idxEl)) return;
        if (!root.TryGetProperty("delta", out var dEl)) return;
        if (!blocks.TryGetValue(idxEl.GetInt32(), out var acc)) return;

        var deltaType = dEl.GetProperty("type").GetString();
        switch (deltaType)
        {
            case "text_delta":
                if (dEl.TryGetProperty("text", out var dt)) acc.Text.Append(dt.GetString());
                break;
            case "input_json_delta":
                if (dEl.TryGetProperty("partial_json", out var pj)) acc.ToolInputJson.Append(pj.GetString());
                break;
            case "thinking_delta":
                if (dEl.TryGetProperty("thinking", out var dth)) acc.Text.Append(dth.GetString());
                break;
            case "signature_delta":
                if (dEl.TryGetProperty("signature", out var sig)) acc.Signature = sig.GetString();
                break;
        }
    }

    private static void HandleMessageDelta(JsonElement root, AggregatedTurn agg)
    {
        // Anthropic streaming places the terminal stop_reason on the
        // `delta` of a `message_delta` event — it never appears anywhere
        // else in the stream. Capturing it here is mandatory: without it
        // the agent loop cannot distinguish end_turn / tool_use / max_tokens
        // / pause_turn / refusal, which is precisely how stop_reason=pause_turn
        // surfaced as a silently empty turn that aborted the agent loop.
        // See https://platform.claude.com/docs/en/build-with-claude/handling-stop-reasons#streaming-considerations
        if (root.TryGetProperty("delta", out var deltaEl) &&
            deltaEl.TryGetProperty("stop_reason", out var srEl) &&
            srEl.ValueKind == JsonValueKind.String)
        {
            agg.StopReason = srEl.GetString();
        }

        if (!root.TryGetProperty("usage", out var u)) return;
        // message_delta carries the running totals; output_tokens in particular
        // converges to its final value here. Cache fields are usually fixed at
        // message_start but we accept later updates defensively.
        if (u.TryGetProperty("output_tokens", out var ot)) agg.OutputTokens = ot.GetInt32();
        if (u.TryGetProperty("input_tokens", out var it)) agg.InputTokens = it.GetInt32();
        if (u.TryGetProperty("cache_read_input_tokens", out var cr)) agg.CacheReadTokens = cr.GetInt32();
        if (u.TryGetProperty("cache_creation_input_tokens", out var cw)) agg.CacheWriteTokens = cw.GetInt32();
    }

    private static void FinalizeBlocks(SortedDictionary<int, BlockAccumulator> blocks, AggregatedTurn agg)
    {
        foreach (var (_, acc) in blocks)
        {
            switch (acc.Type)
            {
                case "text":
                {
                    var text = acc.Text.ToString();
                    agg.FinalText.Append(text);
                    agg.AssistantContent.Add(new AnthropicContentBlock { Type = "text", Text = text });
                    break;
                }
                case "tool_use":
                {
                    // Anthropic streams the tool input as concatenated JSON
                    // fragments via input_json_delta. An empty tool call is
                    // represented by zero deltas — default to an empty object.
                    var json = acc.ToolInputJson.Length == 0 ? "{}" : acc.ToolInputJson.ToString();
                    using var inputDoc = JsonDocument.Parse(json);
                    var input = inputDoc.RootElement.Clone();
                    var id = acc.ToolId ?? string.Empty;
                    var name = acc.ToolName ?? string.Empty;
                    agg.ToolCalls.Add(new PendingToolCall { Id = id, Name = name, Arguments = input });
                    agg.AssistantContent.Add(new AnthropicContentBlock
                    {
                        Type = "tool_use",
                        Id = id,
                        Name = name,
                        Input = input,
                    });
                    break;
                }
                case "thinking":
                    agg.AssistantContent.Add(new AnthropicContentBlock
                    {
                        Type = "thinking",
                        Thinking = acc.Text.ToString(),
                        Signature = acc.Signature,
                    });
                    break;
                case "redacted_thinking":
                    // Round-trip the encrypted payload as Anthropic returned
                    // it. Filtering on Type == "thinking" alone would drop
                    // these blocks and silently break the multi-turn
                    // extended-thinking protocol — surfacing as the model
                    // abruptly halting partway through a long task.
                    agg.AssistantContent.Add(new AnthropicContentBlock
                    {
                        Type = "redacted_thinking",
                        Data = acc.RedactedData,
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// Materializes the (possibly multi-round-trip, pause_turn-aware) agent
    /// turn into the runner-facing <see cref="AgentTurn"/>. Token usage is
    /// summed across every Anthropic round-trip belonging to this turn so
    /// the caller's billing / budget accounting reflects the true cost,
    /// while only the <i>last</i> round-trip's stop_reason determines
    /// whether the model wants tools (<c>tool_use</c>) or has finished
    /// (<c>end_turn</c> / <c>max_tokens</c> / <c>refusal</c>).
    /// </summary>
    private AgentTurn BuildTurn(
        List<PendingToolCall> toolCalls,
        StringBuilder finalText,
        int inputTokens,
        int outputTokens,
        int cacheReadTokens,
        int cacheWriteTokens,
        TimeSpan duration,
        string? requestId,
        string? stopReason)
    {
        // Anthropic reports input_tokens EXCLUSIVE of the cache buckets; AiCostCalculator
        // expects InputTokens to be the inclusive grand total (it subtracts the cache
        // buckets back out). Normalize before costing/reporting so the uncached input
        // isn't dropped to 0 when caching is active. See AiCostCalculator.CalculateInputCost.
        var totalInputTokens = inputTokens + cacheReadTokens + cacheWriteTokens;

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(_context.Llm, new TokenUsage
        {
            InputTokens = totalInputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cacheReadTokens,
            CacheWriteTokens = cacheWriteTokens,
        });

        var usage = new TokenUsage
        {
            InputTokens = totalInputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cacheReadTokens,
            CacheWriteTokens = cacheWriteTokens,
            InputCost = inputCost,
            OutputCost = outputCost,
        };

        // Surface a warning to the caller log when the model truncated on
        // max_tokens or refused mid-turn — these are silent failure modes
        // when the agent loop can't see stop_reason. With the cost cap floor
        // applied by ApplyThinking() this should be exceedingly rare for
        // adaptive thinking, but a refusal can still occur on unrelated
        // policy grounds.
        if (string.Equals(stopReason, "max_tokens", StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Anthropic returned stop_reason=max_tokens on agent turn {Turn} (in={In}, out={Out}). " +
                "The response was truncated — increase MaxTokens or lower thinking effort.",
                _turnIndex, inputTokens, outputTokens);
        }
        else if (string.Equals(stopReason, "refusal", StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Anthropic returned stop_reason=refusal on agent turn {Turn} — the model declined to continue.",
                _turnIndex);
        }

        return new AgentTurn
        {
            ToolCalls = toolCalls,
            FinalText = toolCalls.Count == 0 ? finalText.ToString() : null,
            Usage = usage,
            Duration = duration,
            RequestId = requestId,
        };
    }

    private void AppendInitialUserMessage()
    {
        var prompt = _context.Prompt;
        var userContent = new List<AnthropicContentBlock>();

        if (prompt.Files is not null)
            AppendFiles(userContent, prompt.Files);

        var text = prompt.Text;
        if (_context.ResponseType is { } responseType)
        {
            var schema = AiSchemaRegistry.GetSchema(responseType);
            text += "\n\nWhen you are ready to produce the final answer (after all tool calls), "
                 + "respond with a SINGLE JSON object (no markdown fences) matching this schema:\n"
                 + schema.ToString();
        }

        userContent.Add(new AnthropicContentBlock { Type = "text", Text = text });
        _messages.Add(new AnthropicMessageItem { Role = "user", Content = userContent });
    }

    /// <summary>
    /// Materializes <see cref="AgentSessionContext.InitialChat"/> into the Anthropic
    /// message history. Returns <c>true</c> when the seeded history already ends
    /// on a <see cref="User"/> turn — meaning the model can reply immediately
    /// without us appending the prompt's own initial user message.
    /// </summary>
    private bool AppendInitialChatHistory()
    {
        var chat = _context.InitialChat;
        if (chat is null || chat.Count == 0) return false;

        var i = 0;
        while (i < chat.Count && chat[i] is Assistant) i++;

        var sessionFilesAttached = false;
        var promptFiles = _context.Prompt.Files;

        for (; i < chat.Count; i++)
        {
            switch (chat[i])
            {
                case User u:
                {
                    var userContent = new List<AnthropicContentBlock>();
                    if (!sessionFilesAttached && promptFiles is not null)
                    {
                        AppendFiles(userContent, promptFiles);
                        sessionFilesAttached = true;
                    }
                    if (u.Files is not null) AppendFiles(userContent, u.Files);
                    userContent.Add(new AnthropicContentBlock { Type = "text", Text = u.Text });
                    _messages.Add(new AnthropicMessageItem { Role = "user", Content = userContent });
                    break;
                }
                case Assistant a:
                    _messages.Add(new AnthropicMessageItem
                    {
                        Role = "assistant",
                        Content = new List<AnthropicContentBlock> { new() { Type = "text", Text = a.Text } },
                    });
                    break;
                case Tool t:
                    _messages.Add(new AnthropicMessageItem
                    {
                        Role = "user",
                        Content = new List<AnthropicContentBlock>
                        {
                            new()
                            {
                                Type = "tool_result",
                                ToolUseId = t.ToolCallId,
                                Content = t.ResultJson,
                                IsError = t.IsError ? true : null,
                            },
                        },
                    });
                    break;
            }
        }

        if (_messages.Count == 0) return false;
        return _messages[^1].Role == "user";
    }

    private static void AppendFiles(List<AnthropicContentBlock> content, IReadOnlyList<Asset> files)
    {
        foreach (var file in files.Where(f => f.IsImage))
        {
            content.Add(new AnthropicContentBlock
            {
                Type = "image",
                Source = new AnthropicSource { Type = "base64", MediaType = file.MediaType.Value, Data = file.Base64 },
            });
        }
        foreach (var file in files.Where(f => f.IsDocument && f.MediaType == Asset.MimeType.ApplicationPdf))
        {
            content.Add(new AnthropicContentBlock
            {
                Type = "document",
                Source = new AnthropicSource { Type = "base64", MediaType = file.MediaType.Value, Data = file.Base64 },
            });
        }
    }

    private void AppendToolResultsMessage(IReadOnlyList<ToolResult> toolResults)
    {
        var content = new List<AnthropicContentBlock>(toolResults.Count);
        foreach (var r in toolResults)
        {
            content.Add(new AnthropicContentBlock
            {
                Type = "tool_result",
                ToolUseId = r.CallId,
                Content = r.Output.GetRawText(),
                IsError = r.IsError ? true : null,
            });
        }

        _messages.Add(new AnthropicMessageItem { Role = "user", Content = content });
    }

    private AnthropicMessagesRequest BuildRequest()
    {
        var llm = _context.Llm;
        var prompt = _context.Prompt;

        var request = new AnthropicMessagesRequest
        {
            Model = llm.Name,
            MaxTokens = llm.MaxTokens,
            Messages = _messages,
        };

        var requiredMinTokens = AnthropicProvider.ApplyThinking(llm, request);
        if (request.MaxTokens < requiredMinTokens)
            request.MaxTokens = requiredMinTokens;

        if (llm is IFast { Speed: SpeedType.Fast })
            request.Speed = "fast";

        // When the agent run was seeded from chat[], prompt.Text becomes the system
        // instruction (per IAiProvider.ChatAsync semantics). For standalone agent runs
        // (no chat seed), AppendInitialUserMessage already added prompt.Text as the
        // initial user turn — don't duplicate it as system.
        if (_context.InitialChat is { Count: > 0 } && !string.IsNullOrEmpty(prompt.Text))
            request.System = new List<AnthropicContentBlock> { new() { Type = "text", Text = prompt.Text } };

        if (llm is AnthropicBase anth)
        {
            if (anth.TopP < 1.0) request.TopP = anth.TopP;
            else if (anth.Temperature < 1.0) request.Temperature = anth.Temperature;
        }

        var tools = new List<AnthropicTool>();
        if (llm is AnthropicBase ab && ab.Tools is { Length: > 0 } native)
        {
            // Delegates to the central builder so FunctionTool + WebSearchTool
            // (and any future server tool we wire up) share the same validation
            // path with single-shot calls. Throws NotSupportedException when a
            // model's SupportedTools mask does not advertise the requested
            // capability.
            tools.AddRange(AnthropicProvider.BuildToolsForRequest(llm, native));
        }
        foreach (var custom in _context.Tools)
        {
            tools.Add(new AnthropicTool
            {
                Name = custom.Name,
                Description = custom.Description,
                InputSchema = custom.InputSchema,
            });
        }
        if (tools.Count > 0)
            request.Tools = tools;

        AnthropicProvider.ApplyCaching(llm, request);
        return request;
    }

    public ValueTask DisposeAsync()
    {
        _messages.Clear();
        return ValueTask.CompletedTask;
    }

    /// <summary>Per-block streaming accumulator (text / tool_use input JSON / thinking + signature / redacted_thinking data).</summary>
    private sealed class BlockAccumulator
    {
        public string Type { get; set; } = "";
        public StringBuilder Text { get; } = new();
        public StringBuilder ToolInputJson { get; } = new();
        public string? ToolId { get; set; }
        public string? ToolName { get; set; }
        public string? Signature { get; set; }
        /// <summary>Encrypted payload for <c>redacted_thinking</c> blocks (delivered whole on <c>content_block_start</c>; no deltas follow).</summary>
        public string? RedactedData { get; set; }
    }

    /// <summary>Aggregated state assembled from one stream of SSE events.</summary>
    private sealed class AggregatedTurn
    {
        public string? RequestId { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int CacheReadTokens { get; set; }
        public int CacheWriteTokens { get; set; }
        public List<AnthropicContentBlock> AssistantContent { get; } = new();
        public List<PendingToolCall> ToolCalls { get; } = new();
        public StringBuilder FinalText { get; } = new();
        /// <summary>Terminal stop_reason captured from the message_delta event.</summary>
        public string? StopReason { get; set; }
    }
}

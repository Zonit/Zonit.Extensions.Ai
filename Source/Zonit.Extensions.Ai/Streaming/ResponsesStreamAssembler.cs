using System.Diagnostics;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Reassembles a Responses-API SSE stream into the same response body a non-streaming
/// <c>POST /v1/responses</c> would have returned. Shared by every provider that speaks that
/// wire format — OpenAI, and xAI, which mirrors it.
/// </summary>
/// <remarks>
/// <para>
/// This is what lets <c>GenerateAsync</c> / <c>ChatAsync</c> and the agent loop keep their
/// "one call, one finished result" contract while streaming on the wire. Streaming is not a
/// feature here, it is how a long generation survives: the buffered form holds one HTTP
/// response open for the whole generation, so with <c>max_output_tokens</c> defaulting to the
/// model's full capacity a large answer can outlive the per-attempt timeout — and the retry
/// then restarts generation from zero, at full cost, to fail the same way again.
/// </para>
/// <para>
/// The terminal events (<c>response.completed</c> / <c>response.incomplete</c> /
/// <c>response.failed</c>) each carry the <b>complete</b> Response object — output items,
/// <c>status</c>, <c>incomplete_details</c>, <c>error</c> and <c>usage</c> — so the assembler
/// returns that object verbatim instead of stitching deltas together. Per-delta events serve
/// only as proof of liveness for the watchdog.
/// </para>
/// <para>
/// <b>Truncation is an error, never a partial result.</b> A buffered POST is all-or-nothing
/// for free; a stream is not, so a connection that dies mid-generation must not surface as a
/// successful half-answer.
/// </para>
/// </remarks>
public static class ResponsesStreamAssembler
{
    /// <summary>
    /// Consumes the SSE stream to completion and returns the raw JSON of the terminal Response
    /// object — byte-for-byte what the non-streaming endpoint would have returned.
    /// </summary>
    /// <param name="reader">Reader over the raw SSE body.</param>
    /// <param name="interEventTimeout">Dead-stream watchdog; see <see cref="AiSseReader"/>.</param>
    /// <param name="provider">Provider name, for diagnostics.</param>
    /// <param name="operation">Calling operation, for diagnostics.</param>
    /// <param name="cancellationToken">Caller's token.</param>
    /// <exception cref="TimeoutException">No frame arrived within <paramref name="interEventTimeout"/>.</exception>
    /// <exception cref="HttpRequestException">The stream carried an <c>error</c> event, or ended before a terminal event.</exception>
    public static Task<string> ReadAsync(
        StreamReader reader,
        TimeSpan interEventTimeout,
        string provider,
        string operation,
        CancellationToken cancellationToken = default)
        => ReadAsync(reader, interEventTimeout, TimeSpan.Zero, provider, operation, cancellationToken);

    /// <summary>
    /// Consumes the SSE stream to completion and returns the raw JSON of the terminal Response
    /// object, with a tighter watchdog while the model is writing.
    /// </summary>
    /// <remarks>
    /// Equivalent to the phase-aware overload with the thinking limit left at
    /// <paramref name="interEventTimeout"/>.
    /// </remarks>
    /// <param name="reader">Reader over the raw SSE body.</param>
    /// <param name="interEventTimeout">Limit while the model thinks; see <see cref="AiSseReader"/>.</param>
    /// <param name="outputStallTimeout">
    /// Limit while an output item is streaming and after it closes. Zero or less falls back to
    /// <paramref name="interEventTimeout"/>.
    /// </param>
    /// <param name="provider">Provider name, for diagnostics.</param>
    /// <param name="operation">Calling operation, for diagnostics.</param>
    /// <param name="cancellationToken">Caller's token.</param>
    public static Task<string> ReadAsync(
        StreamReader reader,
        TimeSpan interEventTimeout,
        TimeSpan outputStallTimeout,
        string provider,
        string operation,
        CancellationToken cancellationToken = default)
        => ReadAsync(reader, interEventTimeout, TimeSpan.Zero, outputStallTimeout, provider, operation, cancellationToken);

    /// <summary>
    /// Consumes the SSE stream to completion and returns the raw JSON of the terminal Response
    /// object, with a watchdog whose limit follows the phase the stream is in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Responses stream goes through three kinds of silence, and each gets its own limit:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>Thinking</b> — before the first output item, inside a reasoning item, around a
    ///   server-side tool: <paramref name="thinkingStallTimeout"/>. Reasoning arrives in items a
    ///   few seconds apart even at the highest effort, but a slow server or a very long prompt can
    ///   delay the first one, so this limit is generous.</item>
    ///   <item><b>Writing</b> — a <c>message</c> or <c>function_call</c> item is open and streams
    ///   tokens: <paramref name="outputStallTimeout"/>. A frame arrives every few hundred
    ///   milliseconds, so this silence is a dead stream.</item>
    ///   <item><b>Finishing</b> — a writing item has closed and nothing else is open: only
    ///   <c>response.completed</c> is left, which follows within a second, so
    ///   <paramref name="outputStallTimeout"/> applies again. A reasoning item opened after an
    ///   answer (a model that searches again) returns the stream to thinking.</item>
    /// </list>
    /// <para>
    /// A timeout names the last event, when it arrived and the phase, so a stall can be placed
    /// without a stream trace.
    /// </para>
    /// </remarks>
    /// <param name="reader">Reader over the raw SSE body.</param>
    /// <param name="interEventTimeout">Fallback for a phase whose own limit is zero or less.</param>
    /// <param name="thinkingStallTimeout">Limit while the model thinks. Zero or less: <paramref name="interEventTimeout"/>.</param>
    /// <param name="outputStallTimeout">Limit while it writes or finishes. Zero or less: <paramref name="interEventTimeout"/>.</param>
    /// <param name="provider">Provider name, for diagnostics.</param>
    /// <param name="operation">Calling operation, for diagnostics.</param>
    /// <param name="cancellationToken">Caller's token.</param>
    /// <exception cref="TimeoutException">No frame arrived within the limit of the current phase.</exception>
    /// <exception cref="HttpRequestException">The stream carried an <c>error</c> event, or ended before a terminal event.</exception>
    public static async Task<string> ReadAsync(
        StreamReader reader,
        TimeSpan interEventTimeout,
        TimeSpan thinkingStallTimeout,
        TimeSpan outputStallTimeout,
        string provider,
        string operation,
        CancellationToken cancellationToken = default)
    {
        string? finalResponse = null;
        var openWritingItems = 0;
        var openOtherItems = 0;
        var lastClosedWasWriting = false;

        var clock = Stopwatch.StartNew();
        var lastEvent = "the response headers";
        var lastEventAt = TimeSpan.Zero;

        (TimeSpan Limit, string Knob, string Phase) CurrentPhase()
        {
            var (configured, knob, phase) =
                openWritingItems > 0 ? (outputStallTimeout, "OutputStallTimeout", "writing")
                : openOtherItems > 0 ? (thinkingStallTimeout, "ThinkingStallTimeout", "thinking")
                : lastClosedWasWriting ? (outputStallTimeout, "OutputStallTimeout", "finishing, waiting for response.completed")
                : (thinkingStallTimeout, "ThinkingStallTimeout", "thinking, before any output");

            return configured > TimeSpan.Zero
                ? (configured, knob, phase)
                : (interEventTimeout, "InterEventTimeout", phase);
        }

        try
        {
            await foreach (var data in AiSseReader
                .ReadFramesAsync(reader, () => CurrentPhase().Limit, provider, operation, cancellationToken)
                .ConfigureAwait(false))
            {
                // Terminated by a terminal response.* event; `[DONE]` is tolerated because some
                // gateways synthesize it.
                if (data == "[DONE]") break;

                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeEl)) continue;

                var type = typeEl.GetString();
                lastEventAt = clock.Elapsed;
                lastEvent = ItemType(root) is { } itemType ? $"{type} ({itemType})" : type ?? "(untyped event)";

                switch (type)
                {
                    case "response.completed":
                    case "response.incomplete":
                    case "response.failed":
                        // `incomplete` and `failed` are returned rather than thrown here so the
                        // caller reports them through the same status/error path it uses for the
                        // buffered form (status + incomplete_details.reason + error).
                        if (root.TryGetProperty("response", out var respEl))
                            finalResponse = respEl.GetRawText();
                        break;

                    case "response.output_item.added":
                        if (IsWritingItem(root)) openWritingItems++;
                        else openOtherItems++;
                        break;

                    case "response.output_item.done":
                        if (IsWritingItem(root))
                        {
                            openWritingItems = Math.Max(0, openWritingItems - 1);
                            lastClosedWasWriting = true;
                        }
                        else
                        {
                            openOtherItems = Math.Max(0, openOtherItems - 1);
                            lastClosedWasWriting = false;
                        }
                        break;

                    case "error":
                        throw new HttpRequestException(BuildStreamErrorMessage(root, provider, operation));

                        // Everything else (response.created, response.output_text.delta, …) is
                        // liveness only — the terminal event repeats all of it in assembled form.
                }

                if (finalResponse is not null) break;
            }
        }
        catch (TimeoutException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var (limit, knob, phase) = CurrentPhase();
            throw new TimeoutException(
                $"{provider} {operation} stream produced no event for {Seconds(limit)} after {lastEvent} "
                + $"at {Seconds(lastEventAt)} ({phase}) — server-side stall. "
                + $"Configurable via Ai:Resilience {knob}.",
                ex);
        }

        if (finalResponse is null)
            throw new HttpRequestException(
                HttpRequestError.ResponseEnded,
                $"{provider} {operation} stream ended before a terminal response event (last: {lastEvent} at "
                + $"{Seconds(lastEventAt)}) — the response is incomplete and has been discarded rather "
                + "than parsed as a partial answer.");

        return finalResponse;
    }

    private static string Seconds(TimeSpan value)
        => value.TotalSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s";

    /// <summary>The <c>item.type</c> of an <c>output_item</c> event, or <c>null</c> for any other event.</summary>
    private static string? ItemType(JsonElement root)
        => root.TryGetProperty("item", out var item)
            && item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty("type", out var type)
            ? type.GetString()
            : null;

    /// <summary>
    /// Extracts the text fragment from one SSE payload, or <c>null</c> when the frame is not an
    /// output-text delta. For the live streaming paths (<c>StreamAsync</c> /
    /// <c>ChatStreamAsync</c>), which emit fragments as they arrive instead of assembling them.
    /// </summary>
    /// <remarks>
    /// The Responses API sends text as <c>response.output_text.delta</c> with the fragment in a
    /// top-level <b>string</b> <c>delta</c> field — not the Chat Completions
    /// <c>{"delta":{"text":…}}</c> shape, and not the buffered <c>output[].content[].text</c>
    /// shape. Reading either of those yields an endlessly empty stream.
    /// </remarks>
    public static string? TryReadTextDelta(string data)
    {
        if (data == "[DONE]") return null;

        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeEl)
            || typeEl.GetString() != "response.output_text.delta")
            return null;

        return root.TryGetProperty("delta", out var deltaEl) && deltaEl.ValueKind == JsonValueKind.String
            ? deltaEl.GetString()
            : null;
    }

    /// <summary>
    /// Whether an <c>output_item</c> event concerns an item whose content streams token by token
    /// — answer text or function-call arguments — as opposed to reasoning or a server-side tool,
    /// which may legitimately sit silent.
    /// </summary>
    private static bool IsWritingItem(JsonElement root)
        => ItemType(root) is "message" or "function_call";

    private static string BuildStreamErrorMessage(JsonElement root, string provider, string operation)
    {
        // The top-level `error` event is flat: {"type":"error","code":…,"message":…}.
        var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null;
        var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()
            : null;

        return $"{provider} {operation} stream returned an error event ({code ?? "unknown"}): {message ?? "(no message)"}";
    }
}

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
    /// Two limits, because the stream has two kinds of silence. While the model reasons (or
    /// waits on a server-side tool) nothing may arrive for many minutes, and that is healthy —
    /// <paramref name="interEventTimeout"/> applies. Once an output item that streams tokens is
    /// open (a <c>message</c> or a <c>function_call</c>), a frame arrives every few hundred
    /// milliseconds; a gap of <paramref name="outputStallTimeout"/> there is a dead stream, not
    /// thinking. Without the second limit a stream that froze mid-answer sat for the full
    /// <paramref name="interEventTimeout"/> (30 minutes by default) before anything noticed.
    /// </remarks>
    /// <param name="reader">Reader over the raw SSE body.</param>
    /// <param name="interEventTimeout">Limit outside an open output item; see <see cref="AiSseReader"/>.</param>
    /// <param name="outputStallTimeout">
    /// Limit while an output item is streaming. Zero or less falls back to
    /// <paramref name="interEventTimeout"/>.
    /// </param>
    /// <param name="provider">Provider name, for diagnostics.</param>
    /// <param name="operation">Calling operation, for diagnostics.</param>
    /// <param name="cancellationToken">Caller's token.</param>
    /// <exception cref="TimeoutException">No frame arrived within the limit in effect.</exception>
    /// <exception cref="HttpRequestException">The stream carried an <c>error</c> event, or ended before a terminal event.</exception>
    public static async Task<string> ReadAsync(
        StreamReader reader,
        TimeSpan interEventTimeout,
        TimeSpan outputStallTimeout,
        string provider,
        string operation,
        CancellationToken cancellationToken = default)
    {
        string? finalResponse = null;
        var openWritingItems = 0;

        TimeSpan CurrentLimit() =>
            openWritingItems > 0 && outputStallTimeout > TimeSpan.Zero
                ? outputStallTimeout
                : interEventTimeout;

        await foreach (var data in AiSseReader
            .ReadFramesAsync(reader, CurrentLimit, provider, operation, cancellationToken)
            .ConfigureAwait(false))
        {
            // Terminated by a terminal response.* event; `[DONE]` is tolerated because some
            // gateways synthesize it.
            if (data == "[DONE]") break;

            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) continue;

            switch (typeEl.GetString())
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

                case "response.output_item.added" when IsWritingItem(root):
                    openWritingItems++;
                    break;

                case "response.output_item.done" when IsWritingItem(root):
                    openWritingItems = Math.Max(0, openWritingItems - 1);
                    break;

                case "error":
                    throw new HttpRequestException(BuildStreamErrorMessage(root, provider, operation));

                    // Everything else (response.created, response.output_item.*,
                    // response.output_text.delta, …) is liveness only — the terminal event
                    // repeats all of it in assembled form.
            }

            if (finalResponse is not null) break;
        }

        if (finalResponse is null)
            throw new HttpRequestException(
                HttpRequestError.ResponseEnded,
                $"{provider} {operation} stream ended before a terminal response event — the response is "
                + "incomplete and has been discarded rather than parsed as a partial answer.");

        return finalResponse;
    }

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
        => root.TryGetProperty("item", out var item)
            && item.TryGetProperty("type", out var type)
            && type.GetString() is "message" or "function_call";

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

using System.Text;
using System.Text.Json;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Reassembles Anthropic's SSE event stream into the same <see cref="AnthropicResponse"/>
/// a non-streaming <c>POST /v1/messages</c> would have returned.
/// </summary>
/// <remarks>
/// <para>
/// This is what lets <c>GenerateAsync</c> / <c>ChatAsync</c> keep their "one call,
/// one finished result" contract while using <c>stream: true</c> on the wire. The
/// buffered form cannot be used for real work: it holds a single HTTP response open
/// for the entire generation, so with <c>max_tokens</c> defaulted to the model's full
/// output capacity (128k on Opus / Sonnet 5) a large structured answer routinely
/// outlives any sane per-attempt timeout. Anthropic documents this and their own SDKs
/// refuse to send such a request; the streamed form has no such ceiling because frames
/// arrive continuously.
/// </para>
/// <para>
/// The event handling mirrors the agent loop's proven accumulator
/// (<see cref="AnthropicAgentSession"/>), reduced to what the single-shot paths need:
/// text blocks, <c>tool_use</c> blocks (structured output arrives as the forced
/// <c>respond_json</c> tool's input, streamed as <c>input_json_delta</c> fragments),
/// usage and the terminal <c>stop_reason</c>.
/// </para>
/// <para>
/// <b>Truncation is an error, never a partial result.</b> A buffered POST is
/// all-or-nothing for free; a stream is not, so a connection that dies mid-generation
/// must not surface as a successful half-answer. The stream is only accepted once the
/// terminal <c>message_stop</c> arrives.
/// </para>
/// </remarks>
internal static class AnthropicStreamAssembler
{
    /// <summary>
    /// Consumes the SSE stream to completion and returns the assembled response.
    /// </summary>
    /// <param name="reader">Reader over the raw SSE body.</param>
    /// <param name="interEventTimeout">
    /// Dead-stream watchdog: the maximum gap tolerated between two frames. Once
    /// <c>ResponseHeadersRead</c> has returned, the resilience pipeline is out of
    /// scope, so a server-side stall that still answers transport keep-alives would
    /// otherwise block the read forever.
    /// </param>
    /// <param name="operation">Calling operation, for diagnostics.</param>
    /// <param name="cancellationToken">Caller's token.</param>
    /// <exception cref="TimeoutException">No frame arrived within <paramref name="interEventTimeout"/>.</exception>
    /// <exception cref="HttpRequestException">The stream carried a terminal <c>error</c> event, or ended early.</exception>
    public static async Task<AnthropicResponse> ReadAsync(
        StreamReader reader,
        TimeSpan interEventTimeout,
        string operation,
        CancellationToken cancellationToken)
    {
        var blocks = new SortedDictionary<int, BlockAccumulator>();
        var response = new AnthropicResponse { Usage = new AnthropicUsage() };
        var completed = false;

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
                throw new TimeoutException(
                    $"Anthropic {operation} stream produced no event for {interEventTimeout.TotalSeconds:N0}s — "
                    + "server-side stall (no ping frames). Configurable via Ai:Resilience InterEventTimeout.");
            }

            if (line is null) break;

            // Refresh on every physical line: `event:` headers and blank frame
            // separators are equally proof that the server is still alive.
            watchdog.CancelAfter(interEventTimeout);

            if (line.Length == 0 || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var data = line[6..];

            // Anthropic terminates with `message_stop`; `[DONE]` is tolerated because
            // some gateways synthesize it.
            if (data == "[DONE]")
            {
                completed = true;
                break;
            }

            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) continue;

            switch (typeEl.GetString())
            {
                case "message_start":
                    HandleMessageStart(root, response);
                    break;
                case "content_block_start":
                    HandleBlockStart(root, blocks);
                    break;
                case "content_block_delta":
                    HandleBlockDelta(root, blocks);
                    break;
                case "message_delta":
                    HandleMessageDelta(root, response);
                    break;
                case "message_stop":
                    completed = true;
                    break;
                case "error":
                    throw new HttpRequestException(BuildStreamErrorMessage(root, operation));
                // content_block_stop / ping need no action — ping is the keep-alive
                // that stops an idle intermediary from dropping the connection.
            }

            if (completed) break;
        }

        if (!completed)
            throw new HttpRequestException(
                $"Anthropic {operation} stream ended before the terminal message_stop event — the response is "
                + "incomplete and has been discarded rather than parsed as a partial answer.");

        response.Content = FinalizeBlocks(blocks, operation);
        return response;
    }

    private static string BuildStreamErrorMessage(JsonElement root, string operation)
    {
        if (root.TryGetProperty("error", out var err))
        {
            var type = err.TryGetProperty("type", out var t) ? t.GetString() : null;
            var message = err.TryGetProperty("message", out var m) ? m.GetString() : null;
            return $"Anthropic {operation} stream returned an error event ({type ?? "unknown"}): {message ?? "(no message)"}";
        }

        return $"Anthropic {operation} stream returned an error event with no details.";
    }

    private static void HandleMessageStart(JsonElement root, AnthropicResponse response)
    {
        if (!root.TryGetProperty("message", out var msg)) return;
        if (msg.TryGetProperty("id", out var idEl)) response.Id = idEl.GetString();
        if (msg.TryGetProperty("stop_reason", out var srEl) && srEl.ValueKind == JsonValueKind.String)
            response.StopReason = srEl.GetString();
        if (msg.TryGetProperty("usage", out var u)) ApplyUsage(u, response.Usage!);
    }

    private static void HandleMessageDelta(JsonElement root, AnthropicResponse response)
    {
        // The terminal stop_reason exists ONLY on message_delta in the streamed
        // form — without it the caller cannot tell end_turn from max_tokens /
        // refusal / pause_turn, which is exactly the diagnostic the non-streaming
        // path relied on.
        if (root.TryGetProperty("delta", out var deltaEl) &&
            deltaEl.TryGetProperty("stop_reason", out var srEl) &&
            srEl.ValueKind == JsonValueKind.String)
        {
            response.StopReason = srEl.GetString();
        }

        // Running totals; output_tokens converges to its final value here.
        if (root.TryGetProperty("usage", out var u)) ApplyUsage(u, response.Usage!);
    }

    private static void ApplyUsage(JsonElement usage, AnthropicUsage target)
    {
        if (usage.TryGetProperty("input_tokens", out var it)) target.InputTokens = it.GetInt32();
        if (usage.TryGetProperty("output_tokens", out var ot)) target.OutputTokens = ot.GetInt32();
        if (usage.TryGetProperty("cache_read_input_tokens", out var cr)) target.CacheReadInputTokens = cr.GetInt32();
        if (usage.TryGetProperty("cache_creation_input_tokens", out var cw)) target.CacheCreationInputTokens = cw.GetInt32();
    }

    private static void HandleBlockStart(JsonElement root, SortedDictionary<int, BlockAccumulator> blocks)
    {
        if (!root.TryGetProperty("index", out var idxEl)) return;
        if (!root.TryGetProperty("content_block", out var cbEl)) return;
        if (!cbEl.TryGetProperty("type", out var typeEl)) return;

        var acc = new BlockAccumulator { Type = typeEl.GetString() ?? string.Empty };
        switch (acc.Type)
        {
            case "text":
                if (cbEl.TryGetProperty("text", out var t)) acc.Text.Append(t.GetString());
                break;
            case "tool_use":
                if (cbEl.TryGetProperty("name", out var n)) acc.ToolName = n.GetString();
                // A tool call whose input is already complete at block start carries
                // it inline; later input_json_delta fragments (if any) append to it.
                if (cbEl.TryGetProperty("input", out var inl) && inl.ValueKind == JsonValueKind.Object
                    && inl.EnumerateObject().Any())
                {
                    acc.ToolInputJson.Append(inl.GetRawText());
                }
                break;
        }

        blocks[idxEl.GetInt32()] = acc;
    }

    private static void HandleBlockDelta(JsonElement root, SortedDictionary<int, BlockAccumulator> blocks)
    {
        if (!root.TryGetProperty("index", out var idxEl)) return;
        if (!root.TryGetProperty("delta", out var dEl)) return;
        if (!blocks.TryGetValue(idxEl.GetInt32(), out var acc)) return;
        if (!dEl.TryGetProperty("type", out var dTypeEl)) return;

        switch (dTypeEl.GetString())
        {
            case "text_delta":
                if (dEl.TryGetProperty("text", out var dt)) acc.Text.Append(dt.GetString());
                break;
            case "input_json_delta":
                if (dEl.TryGetProperty("partial_json", out var pj)) acc.ToolInputJson.Append(pj.GetString());
                break;
            // thinking_delta / signature_delta carry no payload the single-shot
            // paths consume; the block itself is still preserved below so the
            // assembled content array has the same shape as the buffered form.
        }
    }

    private static AnthropicContent[] FinalizeBlocks(
        SortedDictionary<int, BlockAccumulator> blocks,
        string operation)
    {
        var content = new List<AnthropicContent>(blocks.Count);

        foreach (var (_, acc) in blocks)
        {
            switch (acc.Type)
            {
                case "text":
                    content.Add(new AnthropicContent { Type = "text", Text = acc.Text.ToString() });
                    break;

                case "tool_use":
                {
                    var json = acc.ToolInputJson.Length == 0 ? "{}" : acc.ToolInputJson.ToString();
                    JsonElement input;
                    try
                    {
                        using var inputDoc = JsonDocument.Parse(json);
                        input = inputDoc.RootElement.Clone();
                    }
                    catch (JsonException ex)
                    {
                        // Concatenated input_json_delta fragments did not form valid
                        // JSON — the stream was cut mid-payload. Failing here is the
                        // point: half a structured answer must never reach the caller.
                        throw new HttpRequestException(
                            $"Anthropic {operation} stream delivered an incomplete tool_use payload "
                            + $"('{acc.ToolName}') — the response was truncated mid-generation.", ex);
                    }

                    content.Add(new AnthropicContent { Type = "tool_use", Name = acc.ToolName, Input = input });
                    break;
                }

                default:
                    // thinking / redacted_thinking and any future block type: keep the
                    // entry so the content array matches the buffered shape, where such
                    // blocks also deserialize without usable text.
                    content.Add(new AnthropicContent { Type = acc.Type });
                    break;
            }
        }

        return [.. content];
    }

    private sealed class BlockAccumulator
    {
        public string Type { get; init; } = string.Empty;
        public StringBuilder Text { get; } = new();
        public StringBuilder ToolInputJson { get; } = new();
        public string? ToolName { get; set; }
    }
}

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Reads the <c>claude -p --output-format stream-json</c> lines of an agent run and reconstructs
/// what the CLI did inside it: how many model round-trips it made, every tool call with its input,
/// result and duration, and the terminal <c>result</c> line.
/// </summary>
/// <remarks>
/// <para>
/// One model response arrives as several <c>assistant</c> lines that share a <c>message.id</c>
/// (one per content block), and the CLI runs a tool as soon as its <c>tool_use</c> block arrives,
/// so its <c>tool_result</c> (a <c>user</c> line) can precede the next block of the same response.
/// Iterations are therefore counted as distinct top-level message ids, which matches the HTTP
/// path, where one iteration is one model round-trip.
/// </para>
/// <para>
/// Lines of a sub-agent (<c>parent_tool_use_id</c> set) belong to the tool that started it and
/// are not counted. Lines that are not JSON are ignored.
/// </para>
/// </remarks>
internal sealed class ClaudeCliRunLog
{
    private readonly string? _bridgeServerName;
    private readonly List<string> _messageIds = [];
    private readonly List<Call> _calls = [];
    private readonly Dictionary<string, Call> _callsById = new(StringComparer.Ordinal);
    private int _unnamedTurns;
    private bool _lastLineWasAssistant;

    /// <param name="bridgeServerName">
    /// MCP server name of the tool bridge (<c>zonit</c>); its tools are reported under their own
    /// names (<c>mcp__zonit__echo</c> → <c>echo</c>), as on the HTTP path. <c>null</c> without a bridge.
    /// </param>
    public ClaudeCliRunLog(string? bridgeServerName) => _bridgeServerName = bridgeServerName;

    /// <summary>The terminal <c>result</c> line, once seen.</summary>
    public ClaudeCliResult? Result { get; private set; }

    /// <summary>Model round-trips seen so far (distinct top-level assistant messages).</summary>
    public int Iterations => _messageIds.Count + _unnamedTurns;

    /// <summary>Consumes one stdout line, received <paramref name="elapsed"/> after the run started.</summary>
    public void Accept(string line, TimeSpan elapsed)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        JsonDocument document;
        try { document = JsonDocument.Parse(line); }
        catch (JsonException) { return; } // diagnostic output, not an event

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            switch (GetString(root, "type"))
            {
                case "assistant":
                    if (!IsSubAgentLine(root)) OnAssistant(root, elapsed);
                    break;
                case "user":
                    if (!IsSubAgentLine(root)) OnUser(root, elapsed);
                    _lastLineWasAssistant = false;
                    break;
                case "result":
                    Result = JsonSerializer.Deserialize(root, AnthropicJsonContext.Default.ClaudeCliResult);
                    break;
            }
        }
    }

    /// <summary>
    /// The tool calls, in the order the model issued them. A call the CLI never reported a result
    /// for (the run ended first) is returned as failed.
    /// </summary>
    public IReadOnlyList<ToolInvocation> ToolCalls()
    {
        var invocations = new ToolInvocation[_calls.Count];
        for (var i = 0; i < _calls.Count; i++)
            invocations[i] = _calls[i].ToInvocation();
        return invocations;
    }

    private void OnAssistant(JsonElement root, TimeSpan elapsed)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            return;

        int iteration;
        if (GetString(message, "id") is { Length: > 0 } id)
        {
            var index = _messageIds.IndexOf(id);
            if (index < 0)
            {
                _messageIds.Add(id);
                index = _messageIds.Count - 1;
            }
            iteration = index + 1;
        }
        else
        {
            // No message id (older CLI builds): a run of consecutive assistant lines is one response.
            if (!_lastLineWasAssistant) _unnamedTurns++;
            iteration = Iterations;
        }
        _lastLineWasAssistant = true;

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return;

        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object || GetString(block, "type") != "tool_use") continue;

            var (name, server) = SplitToolName(GetString(block, "name") ?? "(unnamed)", _bridgeServerName);
            var call = new Call
            {
                Iteration = iteration,
                Name = name,
                McpServer = server,
                Input = block.TryGetProperty("input", out var input) ? input.Clone() : EmptyObject(),
                StartedAt = elapsed,
            };
            _calls.Add(call);
            if (GetString(block, "id") is { Length: > 0 } callId)
                _callsById[callId] = call;
        }
    }

    private void OnUser(JsonElement root, TimeSpan elapsed)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return;
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return;

        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object || GetString(block, "type") != "tool_result") continue;
            if (GetString(block, "tool_use_id") is not { Length: > 0 } callId) continue;
            if (!_callsById.TryGetValue(callId, out var call) || call.Completed) continue;

            var text = block.TryGetProperty("content", out var resultContent) ? ContentText(resultContent) : string.Empty;
            var isError = block.TryGetProperty("is_error", out var flag) && flag.ValueKind == JsonValueKind.True;
            call.Complete(text, isError, elapsed);
        }
    }

    private static bool IsSubAgentLine(JsonElement root)
        => root.TryGetProperty("parent_tool_use_id", out var parent) && parent.ValueKind == JsonValueKind.String;

    /// <summary>
    /// Splits <c>mcp__{server}__{tool}</c>. Bridge tools keep only their own name (they are the
    /// caller's in-process tools); tools of another MCP server name it in <c>McpServer</c>; a
    /// built-in Claude Code tool (<c>Read</c>, <c>Bash</c>…) is returned unchanged.
    /// </summary>
    internal static (string Name, string? McpServer) SplitToolName(string raw, string? bridgeServerName)
    {
        const string prefix = "mcp__";
        if (raw.StartsWith(prefix, StringComparison.Ordinal))
        {
            var separator = raw.IndexOf("__", prefix.Length, StringComparison.Ordinal);
            if (separator > prefix.Length && separator + 2 < raw.Length)
            {
                var server = raw[prefix.Length..separator];
                var tool = raw[(separator + 2)..];
                return string.Equals(server, bridgeServerName, StringComparison.Ordinal) ? (tool, null) : (tool, server);
            }
        }
        return (raw, null);
    }

    /// <summary>A tool result's <c>content</c>: a string, or the text of its content blocks.</summary>
    private static string ContentText(JsonElement content)
    {
        switch (content.ValueKind)
        {
            case JsonValueKind.String:
                return content.GetString() ?? string.Empty;
            case JsonValueKind.Array:
                var sb = new StringBuilder();
                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object || GetString(block, "type") != "text") continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(GetString(block, "text"));
                }
                return sb.ToString();
            case JsonValueKind.Undefined or JsonValueKind.Null:
                return string.Empty;
            default:
                return content.GetRawText();
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    /// <summary>The tool's output as JSON when it is JSON (the bridge sends JSON values as text), otherwise as a JSON string.</summary>
    private static JsonElement ParseOutput(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
                writer.WriteStringValue(text);
            using var document = JsonDocument.Parse(buffer.WrittenMemory);
            return document.RootElement.Clone();
        }
    }

    private sealed class Call
    {
        public required int Iteration { get; init; }
        public required string Name { get; init; }
        public string? McpServer { get; init; }
        public required JsonElement Input { get; init; }
        public required TimeSpan StartedAt { get; init; }

        public bool Completed { get; private set; }
        private string? _text;
        private bool _isError;
        private TimeSpan _duration;

        public void Complete(string text, bool isError, TimeSpan elapsed)
        {
            Completed = true;
            _text = text;
            _isError = isError;
            _duration = elapsed > StartedAt ? elapsed - StartedAt : TimeSpan.Zero;
        }

        public ToolInvocation ToInvocation()
        {
            if (!Completed)
            {
                return new ToolInvocation
                {
                    Iteration = Iteration,
                    Name = Name,
                    McpServer = McpServer,
                    Input = Input,
                    Error = "The Claude CLI reported no result for this tool call (the run ended first).",
                    Duration = TimeSpan.Zero,
                };
            }

            return new ToolInvocation
            {
                Iteration = Iteration,
                Name = Name,
                McpServer = McpServer,
                Input = Input,
                Output = _isError ? null : ParseOutput(_text ?? string.Empty),
                Error = _isError ? (string.IsNullOrEmpty(_text) ? "Tool call failed." : _text) : null,
                Duration = _duration,
            };
        }
    }
}

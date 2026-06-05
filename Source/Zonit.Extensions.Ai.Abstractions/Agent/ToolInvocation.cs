using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Record of a single tool invocation performed during an agent run.
/// Surfaced through <see cref="ResultAgent{T}.ToolCalls"/> for audit, replay
/// or cross-model verification.
/// </summary>
public sealed record ToolInvocation
{
    /// <summary>
    /// The agent iteration (1-based) in which the call was issued by the model.
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Tool name as exposed to the model (may include an MCP prefix, e.g. <c>"github.read_file"</c>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Arguments received from the model (validated against the tool input schema).
    /// </summary>
    public required JsonElement Input { get; init; }

    /// <summary>
    /// Output returned by the tool. <c>null</c> when the tool failed.
    /// </summary>
    public JsonElement? Output { get; init; }

    /// <summary>
    /// Exception message if the tool threw, otherwise <c>null</c>.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Fully-qualified exception type if the tool threw, otherwise <c>null</c>.
    /// </summary>
    public string? ErrorType { get; init; }

    /// <summary>
    /// Wall-clock duration of the tool execution (does not include model round-trip).
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Name of the MCP server that served the call, or <c>null</c> for a locally-executed tool.
    /// </summary>
    public string? McpServer { get; init; }

    /// <summary>
    /// Whether the call was blocked by an <see cref="AgentOptions.OnToolCall"/> hook.
    /// </summary>
    public bool Blocked { get; init; }

    /// <summary>
    /// Convenience: <c>true</c> when the tool failed (exception or blocked).
    /// </summary>
    public bool IsError => Error is not null || Blocked;

    /// <summary>
    /// Token usage of the AI calls this tool made internally (e.g. it injected
    /// <see cref="IAiProvider"/> and called another model). <c>null</c> when the
    /// tool made no AI calls. The full per-call detail is in
    /// <c>ResultAgent&lt;T&gt;.Usage</c> / <c>NestedAiCalls</c>.
    /// </summary>
    public TokenUsage? NestedUsage { get; init; }

    /// <summary>
    /// Cost of the AI this tool consumed internally. <c>null</c> when it made no AI calls.
    /// </summary>
    public Price? NestedCost { get; init; }

    /// <summary>
    /// Number of AI calls this tool made internally (including any sub-agents and
    /// their nested calls). <c>null</c> when the tool made no AI calls.
    /// </summary>
    public int? NestedCalls { get; init; }
}

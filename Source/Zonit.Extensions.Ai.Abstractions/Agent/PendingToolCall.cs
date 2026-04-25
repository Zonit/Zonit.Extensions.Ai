using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// A tool call requested by the model in the current agent turn, awaiting execution.
/// </summary>
/// <remarks>
/// The <see cref="Id"/> is provider-specific (OpenAI <c>call_id</c>, Anthropic <c>tool_use.id</c>)
/// and is used to correlate the subsequent <see cref="ToolResult"/> back to the request.
/// </remarks>
public sealed record PendingToolCall
{
    /// <summary>
    /// Provider-assigned identifier for the call (used to correlate tool results).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Tool name as issued by the model (may include an MCP prefix, e.g. <c>"github.read_file"</c>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Arguments produced by the model, validated against the tool's input schema.
    /// </summary>
    public required JsonElement Arguments { get; init; }
}

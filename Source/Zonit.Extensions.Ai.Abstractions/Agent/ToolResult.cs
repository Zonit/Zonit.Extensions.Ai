using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Result of a single <see cref="PendingToolCall"/> execution, fed back to the
/// model in the next agent turn.
/// </summary>
public sealed record ToolResult
{
    /// <summary>
    /// Correlates with the originating <see cref="PendingToolCall.Id"/>.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Tool name as executed.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Raw JSON output returned by the tool (for successful calls) or a JSON error
    /// object (for failed calls when <see cref="IsError"/> is <c>true</c>).
    /// </summary>
    public required JsonElement Output { get; init; }

    /// <summary>
    /// <c>true</c> when the tool failed (or was blocked). Providers that support
    /// an explicit error flag (e.g. Anthropic <c>is_error</c>) use it; otherwise
    /// the runner embeds the error in <see cref="Output"/> as a structured object.
    /// </summary>
    public bool IsError { get; init; }
}

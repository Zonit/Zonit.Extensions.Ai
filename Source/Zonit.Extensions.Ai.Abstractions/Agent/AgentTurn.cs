namespace Zonit.Extensions.Ai;

/// <summary>
/// Outcome of a single agent iteration (one model round-trip).
/// </summary>
/// <remarks>
/// Exactly one of the two "continuation" shapes is returned per turn:
/// <list type="bullet">
///   <item><description><see cref="ToolCalls"/> non-empty → runner executes them, then calls <c>RunTurnAsync</c> again with the results.</description></item>
///   <item><description><see cref="FinalText"/> non-null → terminal turn; the runner parses the text into <c>TResponse</c> and returns.</description></item>
/// </list>
/// If both are empty/null the runner treats the turn as a no-op termination.
/// </remarks>
public sealed record AgentTurn
{
    /// <summary>
    /// Tool calls the model wants executed before the next iteration.
    /// Empty collection → model produced a final answer.
    /// </summary>
    public required IReadOnlyList<PendingToolCall> ToolCalls { get; init; }

    /// <summary>
    /// Final answer text (raw — structured-output JSON is parsed by the runner).
    /// <c>null</c> when the model requested more tool calls.
    /// </summary>
    public string? FinalText { get; init; }

    /// <summary>
    /// Token usage reported for this single iteration.
    /// </summary>
    public required TokenUsage Usage { get; init; }

    /// <summary>
    /// Wall-clock duration of the model round-trip (not tool execution).
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Provider-assigned request identifier for the iteration, if any.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Tool calls an <b>external</b> runtime already executed during this turn — e.g. the Claude
    /// Code CLI, which runs the whole agent loop itself and calls the framework's tools over the
    /// loopback tool bridge. The runner records them in <c>ResultAgent&lt;T&gt;.ToolCalls</c> and
    /// emits an <see cref="AgentToolCallCompletedEvent"/> for each, but does not execute them
    /// again. <c>null</c> (the usual case) when the runner executes the tools itself through
    /// <see cref="ToolCalls"/>.
    /// </summary>
    public IReadOnlyList<ToolInvocation>? CompletedToolCalls { get; init; }

    /// <summary>
    /// Number of model round-trips this turn covered, when an external runtime ran several of
    /// them inside one turn (the Claude Code CLI reports the whole loop as one turn). The runner
    /// counts it towards <c>ResultAgent&lt;T&gt;.Iterations</c>. <c>null</c> means one.
    /// </summary>
    public int? Iterations { get; init; }
}

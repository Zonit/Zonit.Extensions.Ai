using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Base record for events emitted by <see cref="IAiProvider.StreamAgentAsync{TResponse}"/>.
/// Consumers should pattern-match on the concrete subtype.
/// </summary>
public abstract record AgentEvent
{
    /// <summary>
    /// Agent iteration this event belongs to (1-based).
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>UTC timestamp when the event was produced.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Emitted at the start of each agent iteration, before the model call.
/// </summary>
public sealed record AgentIterationStartedEvent : AgentEvent;

/// <summary>
/// Emitted after the model returned its response for the iteration, before
/// any tool calls are executed.
/// </summary>
public sealed record AgentTurnCompletedEvent : AgentEvent
{
    /// <summary>Usage reported for this single iteration.</summary>
    public required TokenUsage Usage { get; init; }

    /// <summary>Wall-clock duration of the model round-trip.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of tool calls the model requested for this iteration.
    /// Zero means the model produced a final answer.
    /// </summary>
    public required int ToolCallCount { get; init; }

    /// <summary>Provider-assigned request id, if any.</summary>
    public string? RequestId { get; init; }
}

/// <summary>
/// Emitted just before a tool begins executing.
/// </summary>
public sealed record AgentToolCallStartedEvent : AgentEvent
{
    /// <summary>Tool name as exposed to the model.</summary>
    public required string ToolName { get; init; }

    /// <summary>Provider call id for correlation.</summary>
    public required string CallId { get; init; }
}

/// <summary>
/// Emitted after a tool finishes (successfully, with error, or blocked).
/// </summary>
public sealed record AgentToolCallCompletedEvent : AgentEvent
{
    /// <summary>Full invocation record (input, output, error, duration).</summary>
    public required ToolInvocation Invocation { get; init; }
}

/// <summary>
/// Emitted once when the agent produces its final answer, before the
/// terminal <see cref="AgentCompletedEvent{TResponse}"/>.
/// </summary>
public sealed record AgentFinalTextEvent : AgentEvent
{
    /// <summary>Raw final text produced by the model (before JSON parsing).</summary>
    public required string Text { get; init; }
}

/// <summary>
/// Terminal event with the parsed final value and run-wide totals.
/// Always the last event in a successful stream.
/// </summary>
public sealed record AgentCompletedEvent<TResponse> : AgentEvent
{
    /// <summary>Full result — same as the return value of <c>GenerateAsync</c>.</summary>
    public required ResultAgent<TResponse> Result { get; init; }
}

/// <summary>
/// Terminal event emitted when the agent fails (iteration/cost limit,
/// cancellation, unhandled exception with <see cref="ToolExceptionPolicy.ThrowToCaller"/>).
/// Always the last event in a failed stream.
/// </summary>
public sealed record AgentFailedEvent : AgentEvent
{
    /// <summary>Exception that terminated the run.</summary>
    public required Exception Error { get; init; }

    /// <summary>Partial diagnostics (same object as <see cref="AgentException.Partial"/>).</summary>
    public AgentPartialResult? Partial { get; init; }
}

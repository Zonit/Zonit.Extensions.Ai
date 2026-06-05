using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Base exception thrown by the agent loop when it cannot proceed.
/// </summary>
public class AgentException : Exception
{
    /// <summary>
    /// Partial result collected up to the point of failure — iteration count,
    /// tool invocations performed, aggregated token usage. <c>null</c> when
    /// the failure occurred before the first turn.
    /// </summary>
    public AgentPartialResult? Partial { get; }

    /// <inheritdoc/>
    public AgentException(string message, AgentPartialResult? partial = null)
        : base(message)
    {
        Partial = partial;
    }

    /// <inheritdoc/>
    public AgentException(string message, Exception innerException, AgentPartialResult? partial = null)
        : base(message, innerException)
    {
        Partial = partial;
    }
}

/// <summary>
/// Thrown when the agent exceeds its iteration budget.
/// </summary>
public sealed class AgentIterationLimitException : AgentException
{
    /// <summary>Configured maximum.</summary>
    public int Limit { get; }

    /// <inheritdoc/>
    public AgentIterationLimitException(int limit, AgentPartialResult partial)
        : base($"Agent exceeded the iteration limit ({limit}).", partial)
    {
        Limit = limit;
    }
}

/// <summary>
/// Partial run diagnostics surfaced through <see cref="AgentException.Partial"/>.
/// </summary>
public sealed record AgentPartialResult
{
    /// <summary>Iterations completed before failure.</summary>
    public required int Iterations { get; init; }

    /// <summary>Tool calls performed so far (ordered).</summary>
    public required IReadOnlyList<ToolInvocation> ToolCalls { get; init; }

    /// <summary>Aggregated token usage up to the failure.</summary>
    public required TokenUsage TotalUsage { get; init; }

    /// <summary>Aggregated cost up to the failure.</summary>
    public required Price TotalCost { get; init; }
}

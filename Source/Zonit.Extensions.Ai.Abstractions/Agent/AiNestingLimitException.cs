namespace Zonit.Extensions.Ai;

/// <summary>
/// Thrown when an agent run is nested more deeply than the configured limit —
/// e.g. an agent whose tool starts another agent, whose tool starts another
/// agent, beyond <c>AgentOptions.MaxNestedDepth</c> / <c>AiAgentOptions.MaxNestedDepth</c>.
/// </summary>
/// <remarks>
/// This guards against runaway recursion (a tool that re-invokes the same agent
/// it lives in). It is distinct from <see cref="AgentIterationLimitException"/>,
/// which bounds the number of turns <i>within a single</i> agent — not the depth
/// of agent-inside-tool-inside-agent nesting.
/// </remarks>
public sealed class AiNestingLimitException : AgentException
{
    /// <summary>The configured maximum agent nesting depth.</summary>
    public int Limit { get; }

    /// <summary>The depth that was attempted (exceeds <see cref="Limit"/>).</summary>
    public int AttemptedDepth { get; }

    /// <inheritdoc/>
    public AiNestingLimitException(int limit, int attemptedDepth)
        : base($"Agent nesting depth {attemptedDepth} exceeds the configured limit ({limit}). "
             + "A tool started a nested agent too deep — check for unintended recursion, "
             + "or raise AgentOptions.MaxNestedDepth / Ai:Agent:MaxNestedDepth.")
    {
        Limit = limit;
        AttemptedDepth = attemptedDepth;
    }
}

using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// A flat roll-up of AI consumption — tokens, cost, time and the number of model
/// calls. Used on <c>ResultAgent&lt;T&gt;</c> at two levels: <c>Request</c> (the main
/// agent's own turns) and <c>Total</c> (the whole run, including every nested tool
/// and sub-agent call).
/// </summary>
public sealed class AiUsageSummary
{
    /// <summary>Token usage (input/output/cached/reasoning) and the per-token costs.</summary>
    public required TokenUsage Tokens { get; init; }

    /// <summary>Total monetary cost — surfaced explicitly; always equals <see cref="TokenUsage.TotalCost"/>.</summary>
    public Price Cost => Tokens.TotalCost;

    /// <summary>
    /// Summed model wall-clock time. For <c>Total</c> this is the sum across all calls,
    /// so it can exceed the real elapsed time when tool calls ran in parallel (it is a
    /// compute-time figure, not a latency figure).
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Number of model round-trips (one per single-shot call; one per agent turn).</summary>
    public int Calls { get; init; }

    /// <summary>An all-zero summary.</summary>
    public static AiUsageSummary Empty { get; } = new() { Tokens = new TokenUsage() };
}

namespace Zonit.Extensions.Ai;

/// <summary>
/// Result of an agent invocation — extends <see cref="Result{T}"/> with the full
/// trace of tool invocations, the nested AI call tree, and two usage roll-ups.
/// </summary>
/// <typeparam name="T">The final response value type.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Result{T}.MetaData"/> contains metadata for the <b>final</b> model
/// iteration (tokens, cost, duration of the last round-trip).
/// </para>
/// <para>
/// Two usage roll-ups are provided: <see cref="Request"/> (this agent's own model
/// turns) and <see cref="Total"/> (the whole run, <b>including</b> every nested tool
/// and sub-agent AI call). <see cref="Usage"/> is the full tree behind them and
/// <see cref="NestedAiCalls"/> the flat list of nested calls.
/// </para>
/// <para>
/// <see cref="ToolCalls"/> preserves the order of tool invocations (not the
/// order of completion — parallel calls within a single iteration appear in
/// their request order). Persist this collection for audit trails, or feed it
/// to a verifier model.
/// </para>
/// </remarks>
public class ResultAgent<T> : Result<T>
{
    /// <summary>
    /// Number of full agent iterations executed
    /// (one iteration = one model round-trip, possibly followed by tool executions).
    /// </summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// Ordered list of tool invocations performed during the run, including
    /// inputs, outputs, errors, timings and per-tool nested AI usage.
    /// </summary>
    public required IReadOnlyList<ToolInvocation> ToolCalls { get; init; }

    /// <summary>
    /// Usage of <b>this</b> agent only — its own model turns, excluding any AI a tool
    /// or sub-agent ran. Use this to attribute cost to the main agent.
    /// </summary>
    public required AiUsageSummary Request { get; init; }

    /// <summary>
    /// Usage of the <b>whole run, including everything nested</b> (this agent + every
    /// tool call and sub-agent). This is the number for end-to-end cost / quota
    /// accounting. Equals <see cref="Request"/> when no tool invoked AI.
    /// </summary>
    public required AiUsageSummary Total { get; init; }

    /// <summary>
    /// Root of the AI call tree for this run — this agent plus every nested call made
    /// inside its tools (and the tools of any sub-agents). Drill in via
    /// <see cref="AiUsageScope.Children"/>. <c>null</c> only when tracking is unavailable.
    /// </summary>
    public AiUsageScope? Usage { get; init; }

    /// <summary>
    /// Flattened list of every AI call made <i>inside</i> this run's tools (and
    /// sub-agents) — e.g. each <c>ChatAsync</c>/<c>GenerateAsync</c> a tool issued,
    /// with its model, tokens, cost, duration, output and originating
    /// <see cref="AiUsageScope.ToolName"/>. Analogous to <see cref="ToolCalls"/>,
    /// but for nested model calls instead of tools. Empty when no tool invoked AI.
    /// </summary>
    public IReadOnlyList<AiUsageScope> NestedAiCalls { get; init; } = [];
}

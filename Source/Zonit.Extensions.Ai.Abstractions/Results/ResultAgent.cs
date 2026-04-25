using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Result of an agent invocation — extends <see cref="Result{T}"/> with a full
/// trace of tool invocations and aggregated usage across all iterations.
/// </summary>
/// <typeparam name="T">The final response value type.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Result{T}.MetaData"/> contains metadata for the <b>final</b> model
/// iteration (tokens, cost, duration of the last round-trip). The aggregated
/// totals across the entire run are exposed via <see cref="TotalUsage"/> and
/// <see cref="TotalCost"/>.
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
    /// inputs, outputs, errors and timings.
    /// </summary>
    public required IReadOnlyList<ToolInvocation> ToolCalls { get; init; }

    /// <summary>
    /// Total token usage summed across all model iterations.
    /// </summary>
    public required TokenUsage TotalUsage { get; init; }

    /// <summary>
    /// Total cost of the agent run (input + output tokens across all iterations).
    /// </summary>
    public required Price TotalCost { get; init; }
}

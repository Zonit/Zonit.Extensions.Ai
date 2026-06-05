using System.Text.Json.Serialization;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// An immutable node in the AI call tree captured by <see cref="IAiUsageTracker"/>.
/// One node = one logical AI invocation (an agent loop, a single-shot model call)
/// or a tool grouping node. Children are the calls that happened <i>inside</i> this
/// node — e.g. the models a tool invoked, or a nested sub-agent.
/// </summary>
/// <remarks>
/// <para>
/// This is the mechanism that makes nested AI usage visible: when a tool injects
/// <see cref="IAiProvider"/> and calls another model, that call is recorded as a
/// child of the tool's node instead of vanishing. The root node of an agent run is
/// exposed via <c>ResultAgent&lt;T&gt;.Usage</c>; the flattened list of model calls
/// is <c>ResultAgent&lt;T&gt;.NestedAiCalls</c>.
/// </para>
/// <para>
/// <see cref="Usage"/> is this node's <b>own</b> direct usage (e.g. an agent's model
/// turns, or a single call's tokens). The whole-subtree rollup is <see cref="TotalUsage"/>
/// / <see cref="TotalCost"/>. A <see cref="AiUsageKind.Tool"/> node has zero own usage —
/// its cost lives entirely in its children.
/// </para>
/// </remarks>
public sealed class AiUsageScope
{
    /// <summary>Stable identifier for this node (useful for correlating logs / UI).</summary>
    public required Guid Id { get; init; }

    /// <summary>What kind of node this is — see <see cref="AiUsageKind"/>.</summary>
    public required AiUsageKind Kind { get; init; }

    /// <summary>Model name (<c>llm.Name</c>) for model-call and agent nodes; <c>null</c> for tool nodes.</summary>
    public string? Model { get; init; }

    /// <summary>Provider/adapter that served the call (e.g. <c>"AnthropicAgentAdapter"</c>); <c>null</c> for tool nodes.</summary>
    public string? Provider { get; init; }

    /// <summary>
    /// The tool this call belongs to — the nearest tool ancestor's name. For a
    /// <see cref="AiUsageKind.Tool"/> node it is the tool's own name; for a model
    /// call or sub-agent made inside a tool it is inherited, so every node tells you
    /// which tool it came from. <c>null</c> for the root agent and its own turns.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>Provider call id of the originating tool invocation, for correlation with <see cref="ToolInvocation"/>.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Agent iteration (1-based) in which the originating tool was called, if any.</summary>
    public int? ToolIteration { get; init; }

    /// <summary>UTC timestamp when this node started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Wall-clock duration of this node's own work (model round-trip(s)); excludes children for a tool node.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>This node's own token usage and cost (zero for a tool grouping node).</summary>
    public required TokenUsage Usage { get; init; }

    /// <summary>
    /// Number of model round-trips this node performed itself: an agent node's turn
    /// count, <c>1</c> for a single-shot call, <c>0</c> for a tool grouping node.
    /// </summary>
    public int Calls { get; init; }

    /// <summary>Provider-assigned request id, if any.</summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Input prompt (or a preview of it) sent to the model. Captured only when
    /// <c>AiAgentOptions.CaptureNestedIo</c> is enabled; otherwise <c>null</c>.
    /// </summary>
    public string? Input { get; init; }

    /// <summary>
    /// Output produced by the model (final text / serialized value, or a preview).
    /// Captured only when <c>AiAgentOptions.CaptureNestedIo</c> is enabled; otherwise <c>null</c>.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>Direct child calls made inside this node, in execution order.</summary>
    public required IReadOnlyList<AiUsageScope> Children { get; init; }

    /// <summary>Total token usage of this node plus its entire subtree.</summary>
    [JsonIgnore]
    public TokenUsage TotalUsage =>
        TokenUsageMath.Add(Usage, TokenUsageMath.Sum(Children.Select(c => c.TotalUsage)));

    /// <summary>Total cost of this node plus its entire subtree.</summary>
    [JsonIgnore]
    public Price TotalCost => TotalUsage.TotalCost;

    /// <summary>Total model round-trips of this node plus its entire subtree.</summary>
    [JsonIgnore]
    public int TotalCalls
    {
        get
        {
            var total = Calls;
            foreach (var c in Children)
                total += c.TotalCalls;
            return total;
        }
    }

    /// <summary>Total wall-clock duration of this node plus its entire subtree.</summary>
    [JsonIgnore]
    public TimeSpan TotalDuration
    {
        get
        {
            var total = Duration;
            foreach (var c in Children)
                total += c.TotalDuration;
            return total;
        }
    }

    /// <summary>
    /// Enumerates every descendant node (children, grandchildren, …) in pre-order
    /// execution order. Does not include this node itself.
    /// </summary>
    public IEnumerable<AiUsageScope> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var d in child.Descendants())
                yield return d;
        }
    }

    /// <summary>
    /// Flattened list of the model calls made inside this node (descendants whose
    /// <see cref="Kind"/> is not <see cref="AiUsageKind.Tool"/>), in execution order.
    /// This is the "list of sub-models that ran" — analogous to the tool-call list.
    /// </summary>
    public IReadOnlyList<AiUsageScope> ModelCalls()
        => Descendants().Where(n => n.Kind != AiUsageKind.Tool).ToList();
}

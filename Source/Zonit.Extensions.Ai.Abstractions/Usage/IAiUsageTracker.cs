namespace Zonit.Extensions.Ai;

/// <summary>
/// Ambient tracker that records the tree of AI calls made within the current
/// logical operation — including nested calls made by tools (and tools of
/// sub-agents). Registered as a singleton; the per-operation state flows with
/// the async context, so concurrent requests are isolated automatically.
/// </summary>
/// <remarks>
/// <para>
/// The framework wires this in transparently: every call through
/// <see cref="IAiProvider"/> attaches itself to the call tree of whatever agent
/// run (or scope) is currently executing. Application code normally reads the
/// result via <c>ResultAgent&lt;T&gt;.Usage</c> / <c>NestedAiCalls</c> rather
/// than this interface.
/// </para>
/// <para>
/// This interface is intentionally read-only. The push/pop/record operations
/// that build the tree are an internal concern of the framework.
/// </para>
/// </remarks>
public interface IAiUsageTracker
{
    /// <summary>
    /// <c>true</c> when a tracking scope is currently active on this async flow
    /// (i.e. code is running inside an agent run or an explicitly opened scope).
    /// </summary>
    bool IsTracking { get; }

    /// <summary>
    /// An immutable snapshot of the current node's subtree, or <c>null</c> when no
    /// scope is active. Useful for a tool that wants to inspect how much AI budget
    /// it has consumed so far (e.g. <c>tracker.CurrentSnapshot?.TotalCost</c>).
    /// </summary>
    AiUsageScope? CurrentSnapshot { get; }
}

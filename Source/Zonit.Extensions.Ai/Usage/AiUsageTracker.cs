namespace Zonit.Extensions.Ai;

/// <summary>
/// Default <see cref="IAiUsageTracker"/>. The ambient "current node" lives in an
/// <see cref="AsyncLocal{T}"/>, so the singleton instance carries no shared mutable
/// state and concurrent requests are isolated by their async flow. Each top-level
/// call starts from <c>Current == null</c> and builds its own tree.
/// </summary>
/// <remarks>
/// The read-only surface (<see cref="IsTracking"/>, <see cref="CurrentSnapshot"/>)
/// is public via <see cref="IAiUsageTracker"/>. The push/record/pop methods below
/// are internal — only the framework (AgentRunner, ToolExecutor, AiProvider) drives
/// the tree.
/// </remarks>
internal sealed class AiUsageTracker : IAiUsageTracker
{
    private readonly AsyncLocal<AiUsageScopeBuilder?> _current = new();

    /// <inheritdoc />
    public bool IsTracking => _current.Value is not null;

    /// <inheritdoc />
    public AiUsageScope? CurrentSnapshot => _current.Value?.Freeze();

    /// <summary>The current node, or <c>null</c> at the top level.</summary>
    internal AiUsageScopeBuilder? Current => _current.Value;

    /// <summary>
    /// Opens a new node as a child of the current node (or a root when none is active)
    /// and makes it current. For <see cref="AiUsageKind.Agent"/> nodes the nesting
    /// depth is checked against <paramref name="maxDepth"/> before anything is mutated
    /// (so a rejected open leaves the tree untouched).
    /// </summary>
    public AiUsageScopeBuilder BeginScope(
        AiUsageKind kind,
        string? model,
        string? provider,
        string? toolName = null,
        string? toolCallId = null,
        int? toolIteration = null,
        int? maxDepth = null)
    {
        var parent = _current.Value;

        if (kind == AiUsageKind.Agent && maxDepth is int limit && limit > 0)
        {
            var depth = AgentDepth(parent) + 1; // +1 for the node we are about to open
            if (depth > limit)
                throw new AiNestingLimitException(limit, depth);
        }

        var node = new AiUsageScopeBuilder(parent, kind, model, provider, toolName, toolCallId, toolIteration);
        parent?.AddChild(node);
        _current.Value = node;
        return node;
    }

    /// <summary>
    /// Opens a new node under an <b>explicit</b> parent (rather than the ambient
    /// current) and makes it current. Used at the agent↔tool boundary: tools run on
    /// parallel <c>Task.Run</c> branches where relying on ambient flow from the agent
    /// loop is fragile, so the parent agent node is threaded in directly. Setting
    /// current here is reliable because it happens inside the tool's own branch — the
    /// tool body and any AI call it makes read it correctly.
    /// </summary>
    public AiUsageScopeBuilder BeginChild(
        AiUsageScopeBuilder parent,
        AiUsageKind kind,
        string? model = null,
        string? provider = null,
        string? toolName = null,
        string? toolCallId = null,
        int? toolIteration = null)
    {
        var node = new AiUsageScopeBuilder(parent, kind, model, provider, toolName, toolCallId, toolIteration);
        parent.AddChild(node);
        _current.Value = node;
        return node;
    }

    /// <summary>Records own usage into <paramref name="scope"/> (see <see cref="AiUsageScopeBuilder.Record"/>).</summary>
    public void Record(
        AiUsageScopeBuilder scope,
        TokenUsage usage,
        TimeSpan duration,
        string? requestId = null,
        string? input = null,
        string? output = null)
        => scope.Record(usage, duration, requestId, input, output);

    /// <summary>Sets a node's output text without counting a model call (see <see cref="AiUsageScopeBuilder.SetOutput"/>).</summary>
    public void SetOutput(AiUsageScopeBuilder scope, string? output) => scope.SetOutput(output);

    /// <summary>
    /// Restores the parent of <paramref name="scope"/> as current. Safe to call on
    /// any exit path (normal, exception, abandoned enumerator).
    /// </summary>
    public void EndScope(AiUsageScopeBuilder scope)
        => _current.Value = scope.Parent;

    private static int AgentDepth(AiUsageScopeBuilder? node)
    {
        var depth = 0;
        for (var n = node; n is not null; n = n.Parent)
        {
            if (n.Kind == AiUsageKind.Agent)
                depth++;
        }
        return depth;
    }
}

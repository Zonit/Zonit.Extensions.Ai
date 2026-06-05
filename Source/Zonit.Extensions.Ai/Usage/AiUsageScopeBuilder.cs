namespace Zonit.Extensions.Ai;

/// <summary>
/// Mutable accumulator for one node of the AI call tree, used by
/// <see cref="AiUsageTracker"/> while a run is in flight. Frozen into an
/// immutable <see cref="AiUsageScope"/> when the run completes.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safety contract: the only operation that can race is <see cref="AddChild"/>
/// — sibling tools execute in parallel and add children to the same parent node.
/// That is synchronized with <see cref="_gate"/>. The node's <i>own</i> fields
/// (<see cref="Record"/>) are single-writer: an agent node is written only by its
/// sequential turn loop; a leaf node only by its own single call. So they need no
/// lock. <see cref="Freeze"/> is called only after the node's work (and any child
/// <c>Task.WhenAll</c> barrier) has completed.
/// </para>
/// </remarks>
internal sealed class AiUsageScopeBuilder
{
    private readonly object _gate = new();
    private readonly List<AiUsageScopeBuilder> _children = new();
    private readonly Guid _id = Guid.NewGuid();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    private TokenUsage _usage = TokenUsageMath.Zero;
    private TimeSpan _duration = TimeSpan.Zero;
    private int _calls;
    private string? _requestId;
    private string? _input;
    private string? _output;

    public AiUsageScopeBuilder(
        AiUsageScopeBuilder? parent,
        AiUsageKind kind,
        string? model,
        string? provider,
        string? toolName,
        string? toolCallId,
        int? toolIteration)
    {
        Parent = parent;
        Kind = kind;
        Model = model;
        Provider = provider;
        // Inherit the tool context from the nearest tool ancestor when not set
        // explicitly, so every node (and thus every NestedAiCalls entry) is
        // self-describing: "this Chat call came from tool X, iteration N".
        ToolName = toolName ?? parent?.ToolName;
        ToolCallId = toolCallId ?? parent?.ToolCallId;
        ToolIteration = toolIteration ?? parent?.ToolIteration;
    }

    public AiUsageScopeBuilder? Parent { get; }
    public AiUsageKind Kind { get; }
    public string? Model { get; }

    /// <summary>Settable: the agent adapter is chosen after the scope is opened.</summary>
    public string? Provider { get; set; }

    public string? ToolName { get; }
    public string? ToolCallId { get; }
    public int? ToolIteration { get; }

    /// <summary>Adds a child node. Synchronized — parallel tools share one parent.</summary>
    public void AddChild(AiUsageScopeBuilder child)
    {
        lock (_gate)
            _children.Add(child);
    }

    /// <summary>
    /// Records one model round-trip's own usage (and increments the call count).
    /// Called once for a leaf call, or once per turn for an agent node (single-writer
    /// either way). The first non-null <paramref name="input"/> is kept; the last
    /// non-null <paramref name="output"/> wins.
    /// </summary>
    public void Record(
        TokenUsage usage,
        TimeSpan duration,
        string? requestId = null,
        string? input = null,
        string? output = null)
    {
        _usage = TokenUsageMath.Add(_usage, usage);
        _duration += duration;
        _calls++;
        if (requestId is not null) _requestId = requestId;
        if (input is not null && _input is null) _input = input;
        if (output is not null) _output = output;
    }

    /// <summary>
    /// Sets the output text without counting a call — used for an agent's final text,
    /// whose round-trip was already counted by the terminal turn's <see cref="Record"/>.
    /// </summary>
    public void SetOutput(string? output)
    {
        if (output is not null) _output = output;
    }

    /// <summary>Produces an immutable snapshot of this node and its subtree.</summary>
    public AiUsageScope Freeze()
    {
        AiUsageScopeBuilder[] snapshot;
        lock (_gate)
            snapshot = _children.ToArray();

        var children = new AiUsageScope[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
            children[i] = snapshot[i].Freeze();

        return new AiUsageScope
        {
            Id = _id,
            Kind = Kind,
            Model = Model,
            Provider = Provider,
            ToolName = ToolName,
            ToolCallId = ToolCallId,
            ToolIteration = ToolIteration,
            StartedAt = _startedAt,
            Duration = _duration,
            Usage = _usage,
            Calls = _calls,
            RequestId = _requestId,
            Input = _input,
            Output = _output,
            Children = children,
        };
    }
}

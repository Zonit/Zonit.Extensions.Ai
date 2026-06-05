namespace Zonit.Extensions.Ai;

/// <summary>
/// Per-invocation options for agent calls. Null-valued properties fall back
/// to the global <c>Ai:Agent</c> configuration (<c>AiAgentOptions</c>) or,
/// in the case of <see cref="MaxIterations"/>, to the model's
/// <see cref="IAgentLlm.DefaultMaxIterations"/>.
/// </summary>
public sealed class AgentOptions
{
    /// <summary>
    /// Hard ceiling for the number of agent iterations in this call.
    /// Null = use the global / model default.
    /// </summary>
    public int? MaxIterations { get; init; }

    /// <summary>
    /// Maximum number of tool calls executed in parallel within a single turn.
    /// When the model returns more tool calls in one turn, the executor queues
    /// the surplus and runs them as worker slots free up — every tool call is
    /// always executed, no call is ever rejected. Null = use the global default.
    /// </summary>
    public int? MaxParallelToolCalls { get; init; }

    /// <summary>
    /// Hard wall-clock timeout for the entire agent call.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Controls whether tools registered globally via
    /// <c>services.AddAiTools(...)</c> are exposed to the model when the
    /// caller did <b>not</b> supply an explicit <c>tools:</c> list (i.e.
    /// passed <c>null</c>). Default <c>true</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Passing an explicit <c>tools:</c> list (including an empty one) is
    /// authoritative — DI defaults are <b>never</b> merged on top, regardless
    /// of this flag. <c>tools: []</c> therefore means "no tools for this
    /// call, period" and <c>tools: [t1]</c> means "exactly <c>t1</c>, nothing
    /// else from the container".
    /// </para>
    /// <para>
    /// Set this flag to <c>false</c> only when you want to suppress DI
    /// defaults <i>even though</i> you passed <c>tools: null</c> — useful for
    /// fully provider-driven calls where the model should rely solely on its
    /// built-in / MCP tooling.
    /// </para>
    /// </remarks>
    public bool DefaultTools { get; init; } = true;

    /// <summary>
    /// Controls whether MCP servers registered globally via
    /// <c>services.AddAiMcp(...)</c> are attached when the caller did
    /// <b>not</b> supply an explicit <c>mcps:</c> list (i.e. passed
    /// <c>null</c>). Default <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Passing an explicit <c>mcps:</c> list (including an empty one) is
    /// authoritative — DI-registered MCP servers are <b>never</b> merged on
    /// top, regardless of this flag. Set this flag to <c>false</c> only to
    /// suppress DI-registered MCPs when you also passed <c>mcps: null</c>.
    /// </remarks>
    public bool DefaultMcp { get; init; } = true;

    /// <summary>
    /// Allow-list of tool names. When set, only tools whose <see cref="ITool.Name"/>
    /// (including the MCP prefix, e.g. <c>"github.read_file"</c>) appears in this
    /// collection are exposed to the model for this invocation.
    /// </summary>
    public IReadOnlyCollection<string>? AllowedTools { get; init; }

    /// <summary>
    /// Called before every tool execution. Returning <c>false</c> blocks the call;
    /// the model receives a tool result with <c>"blocked by policy"</c> and decides
    /// whether to retry differently.
    /// </summary>
    public Func<ToolInvocation, CancellationToken, ValueTask<bool>>? OnToolCall { get; init; }

    /// <summary>
    /// Maximum depth of nested agent runs (an agent whose tool starts another
    /// agent, and so on). Exceeding it throws <see cref="AiNestingLimitException"/>.
    /// Null = use the global <c>Ai:Agent:MaxNestedDepth</c>. A value &lt;= 0 disables
    /// the guard.
    /// </summary>
    /// <remarks>
    /// This bounds <i>nesting</i> (agent → tool → agent → …), which
    /// <see cref="MaxIterations"/> does not — that bounds turns within a single
    /// agent. The depth counts agent nodes only; the root agent is depth 1.
    /// </remarks>
    public int? MaxNestedDepth { get; init; }
}

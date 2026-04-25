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
    /// When <c>true</c> (default), tools registered globally via
    /// <c>services.AddAiTools(...)</c> are added to the tool set the model
    /// sees in this call. Set to <c>false</c> to opt out of every default
    /// tool for this single invocation — only tools passed in the
    /// <c>tools:</c> argument will be exposed.
    /// </summary>
    public bool DefaultTools { get; init; } = true;

    /// <summary>
    /// When <c>true</c> (default), MCP servers registered globally via
    /// <c>services.AddAiMcp(...)</c> are added to this call. Set to
    /// <c>false</c> to opt out of every default MCP server for this single
    /// invocation — only MCPs passed in the <c>mcps:</c> argument will be used.
    /// </summary>
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
}

using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Fluent builder for an agent run. Obtain one from
/// <see cref="IAiProvider.Agent{TResponse}(IAgentLlm, IPrompt{TResponse})"/> and terminate with
/// <see cref="RunAsync"/> (awaited result) or <see cref="RunStreamAsync"/> (event stream).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tools are empty by default.</b> Nothing reaches the model unless you add it explicitly with
/// <see cref="AddTool{TTool}"/> / <see cref="AddTool(ITool)"/>, or opt into the globally registered
/// set with <see cref="AddDefaultTools"/>. The same holds for MCP servers
/// (<see cref="AddMcp"/> / <see cref="AddDefaultMcp"/>). This is the safe-by-default counterpart of
/// the positional API, where globally registered tooling is also off unless requested.
/// </para>
/// <para>
/// Trusted server data (the current user / tenant / permission scope) goes through
/// <see cref="WithContext"/>, never through the model — it is matched to each scoped tool's
/// <c>TScope</c> by type. See <c>ToolBase&lt;TScope, TInput, TOutput&gt;</c>.
/// </para>
/// </remarks>
public interface IAgentRequest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>
{
    /// <summary>Adds a tool resolved from DI (its dependencies are injected by the container).</summary>
    IAgentRequest<TResponse> AddTool<TTool>() where TTool : class, ITool;

    /// <summary>Adds an already-constructed tool instance (handy in tests / scripts).</summary>
    IAgentRequest<TResponse> AddTool(ITool tool);

    /// <summary>Adds several already-constructed tool instances.</summary>
    IAgentRequest<TResponse> AddTools(IEnumerable<ITool> tools);

    /// <summary>
    /// Opts this call into the globally registered tool set (everything added via
    /// <c>AddAiTools&lt;T&gt;()</c>). Off unless called. Composes with explicit
    /// <see cref="AddTool{TTool}"/> calls — the union is exposed.
    /// </summary>
    IAgentRequest<TResponse> AddDefaultTools();

    /// <summary>
    /// Attaches an MCP server for this call. <paramref name="configure"/> sets per-server options
    /// (e.g. <c>o =&gt; o.AllowOnly("read_file")</c>) without mixing them into the request's own chain.
    /// </summary>
    IAgentRequest<TResponse> AddMcp(string name, string url, string? token = null, Action<IMcpOptions>? configure = null);

    /// <summary>Opts this call into the globally registered MCP servers (added via <c>AddAiMcp(...)</c>). Off unless called.</summary>
    IAgentRequest<TResponse> AddDefaultMcp();

    /// <summary>
    /// Supplies trusted server data delivered to scoped tools by <c>TScope</c> type, never sent to
    /// the model. Call once per distinct context type the exposed scoped tools require.
    /// </summary>
    IAgentRequest<TResponse> WithContext(object context);

    /// <summary>Restricts the model to tools whose name appears in <paramref name="toolNames"/> (incl. <c>"{mcp}.{tool}"</c>).</summary>
    IAgentRequest<TResponse> AllowOnly(params string[] toolNames);

    /// <summary>Gate run before each tool execution; returning <c>false</c> blocks that call.</summary>
    IAgentRequest<TResponse> OnToolCall(Func<ToolInvocation, CancellationToken, ValueTask<bool>> handler);

    /// <summary>Hard ceiling on agent iterations for this run.</summary>
    IAgentRequest<TResponse> MaxIterations(int max);

    /// <summary>Concurrency for tool execution within a turn (surplus is queued, never dropped).</summary>
    IAgentRequest<TResponse> MaxParallelToolCalls(int max);

    /// <summary>Wall-clock timeout for the whole run.</summary>
    IAgentRequest<TResponse> Timeout(TimeSpan timeout);

    /// <summary>Bounds agent → tool → agent nesting depth.</summary>
    IAgentRequest<TResponse> MaxNestedDepth(int depth);

    /// <summary>Runs the agent and returns the final structured answer plus the full audit trail.</summary>
    Task<ResultAgent<TResponse>> RunAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs the agent and streams <see cref="AgentEvent"/>s (iteration / tool-call / final / failed).</summary>
    IAsyncEnumerable<AgentEvent> RunStreamAsync(CancellationToken cancellationToken = default);
}

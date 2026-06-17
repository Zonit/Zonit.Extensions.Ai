namespace Zonit.Extensions.Ai;

/// <summary>
/// Exposes the framework's in-process <see cref="ITool"/> set to an external, local
/// agent runtime — primarily the Claude Code CLI (<c>claude -p</c>) — over a secured
/// loopback MCP (Model Context Protocol) server.
/// </summary>
/// <remarks>
/// <para>
/// The Claude Code CLI runs its own agentic loop and executes tools itself; the only
/// way it can invoke our C# tools is via an MCP server. Because the CLI is a local
/// subprocess on the same machine, the server is hosted on <c>127.0.0.1</c> with an
/// ephemeral port and a per-session bearer token — it is never reachable off-machine.
/// </para>
/// <para>
/// This abstraction lives in the abstractions assembly so providers depend only on it;
/// the concrete implementation (with its hosting dependencies) ships in the opt-in
/// <c>Zonit.Extensions.Ai.Mcp.Server</c> package and is registered via
/// <c>AddAiAgentToolBridge()</c>. When no implementation is registered, tool-using
/// agents cannot run over the CLI (the provider surfaces a clear error or falls back to
/// the HTTP API, per the configured transport mode).
/// </para>
/// </remarks>
public interface IAgentToolBridge
{
    /// <summary>
    /// Publishes <paramref name="tools"/> on a secured loopback MCP endpoint and returns
    /// a session describing how a local MCP client (e.g. <c>claude -p --mcp-config</c>)
    /// reaches it. Dispose the returned session when the agent run completes to revoke
    /// access.
    /// </summary>
    /// <param name="tools">The tools to expose for this agent run (custom C# tools and/or MCP-proxy tools).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IAgentToolBridgeSession> StartAsync(IReadOnlyList<ITool> tools, CancellationToken cancellationToken);
}

/// <summary>
/// A live publication of a tool set on the loopback MCP bridge. Disposing it revokes the
/// session's token so the tools are no longer reachable.
/// </summary>
public interface IAgentToolBridgeSession : IAsyncDisposable
{
    /// <summary>
    /// Logical MCP server name used to namespace the tools to the model (the CLI exposes
    /// them as <c>mcp__{ServerName}__{tool}</c>). Used when composing <c>--mcp-config</c>
    /// and <c>--allowedTools</c>.
    /// </summary>
    string ServerName { get; }

    /// <summary>Loopback MCP endpoint URL (e.g. <c>http://127.0.0.1:&lt;port&gt;/mcp</c>).</summary>
    Uri Url { get; }

    /// <summary>
    /// Bearer token the client must present (<c>Authorization: Bearer &lt;token&gt;</c>) to
    /// reach this session's tools. <c>null</c> only if the implementation forgoes token auth.
    /// </summary>
    string? AuthToken { get; }

    /// <summary>The names of the published tools (as the model/CLI will see them, pre-namespacing).</summary>
    IReadOnlyList<string> ToolNames { get; }
}

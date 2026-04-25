namespace Zonit.Extensions.Ai;

/// <summary>
/// Builds <see cref="ITool"/> adapters that proxy calls to external MCP servers
/// over HTTP / SSE. Defined in <c>Abstractions</c> so the core runner can call
/// into MCP functionality without taking a hard dependency on the MCP client
/// implementation.
/// </summary>
/// <remarks>
/// The default implementation shipped with <c>Zonit.Extensions.Ai</c> performs
/// a no-op and returns an empty list — the concrete HTTP/SSE JSON-RPC client
/// lives in the <c>Zonit.Extensions.Ai.Mcp</c> package and replaces the
/// registration when added.
/// </remarks>
public interface IMcpToolFactory
{
    /// <summary>
    /// Connects to each server, lists its tools, and returns <see cref="ITool"/>
    /// adapters the agent runner can treat uniformly with local tools.
    /// </summary>
    /// <param name="servers">MCP servers to probe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ITool>> BuildAsync(
        IReadOnlyList<Mcp> servers,
        CancellationToken cancellationToken);
}

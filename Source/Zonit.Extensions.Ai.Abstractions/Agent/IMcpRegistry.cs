namespace Zonit.Extensions.Ai;

/// <summary>
/// Registry of external MCP servers configured in the DI container.
/// Populated by <c>AddAiMcp(Mcp)</c>.
/// </summary>
/// <remarks>
/// The agent runner queries this registry only when the caller does not pass
/// an explicit <c>mcps</c> argument to <c>GenerateAsync</c>. Passing an explicit
/// list <b>overrides</b> the DI registry for that single invocation.
/// </remarks>
public interface IMcpRegistry
{
    /// <summary>
    /// Returns a snapshot of all registered MCP servers.
    /// </summary>
    IReadOnlyList<Mcp> GetAll();

    /// <summary>
    /// Attempts to resolve a server by its <see cref="Mcp.Name"/>.
    /// </summary>
    Mcp? Get(string name);
}

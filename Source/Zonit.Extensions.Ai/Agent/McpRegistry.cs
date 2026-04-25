using System.Collections.Concurrent;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Default <see cref="IMcpRegistry"/> implementation backed by the
/// <see cref="Mcp"/> descriptors registered in the DI container.
/// </summary>
internal sealed class McpRegistry : IMcpRegistry
{
    private readonly IReadOnlyList<Mcp> _servers;
    private readonly ConcurrentDictionary<string, Mcp> _byName;

    public McpRegistry(IEnumerable<Mcp> servers)
    {
        _servers = servers.ToArray();
        _byName = new ConcurrentDictionary<string, Mcp>(StringComparer.Ordinal);

        foreach (var server in _servers)
        {
            if (!_byName.TryAdd(server.Name, server))
            {
                throw new InvalidOperationException(
                    $"Duplicate MCP server name '{server.Name}'. MCP names must be unique.");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Mcp> GetAll() => _servers;

    /// <inheritdoc />
    public Mcp? Get(string name)
        => _byName.TryGetValue(name, out var mcp) ? mcp : null;
}

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Ai;

/// <summary>
/// <see cref="IMcpToolFactory"/> implementation that spins up an
/// <see cref="McpClient"/> per server, lists the tools, and returns
/// <see cref="McpTool"/> adapters.
/// </summary>
/// <remarks>
/// Clients are created per <c>BuildAsync</c> call (one agent run). Disposal
/// of clients happens when the caller disposes the returned tools (via the
/// <c>IAsyncDisposable</c> contract of the underlying clients). In practice,
/// the <see cref="AgentRunner"/> owns the scope and the clients live for the
/// duration of one <c>GenerateAsync</c>.
/// </remarks>
[RequiresUnreferencedCode("JSON serialization requires types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization requires runtime code generation.")]
public sealed class McpToolFactory : IMcpToolFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<McpToolFactory> _logger;

    public McpToolFactory(IHttpClientFactory httpClientFactory, ILogger<McpToolFactory> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ITool>> BuildAsync(
        IReadOnlyList<Mcp> servers,
        CancellationToken cancellationToken)
    {
        if (servers.Count == 0)
            return Array.Empty<ITool>();

        // Build clients concurrently — each server is independent.
        var tasks = new Task<IReadOnlyList<ITool>>[servers.Count];
        for (var i = 0; i < servers.Count; i++)
        {
            var server = servers[i];
            tasks[i] = BuildSingleAsync(server, cancellationToken);
        }

        var perServer = await Task.WhenAll(tasks).ConfigureAwait(false);

        var all = new List<ITool>();
        foreach (var set in perServer) all.AddRange(set);
        return all;
    }

    private async Task<IReadOnlyList<ITool>> BuildSingleAsync(Mcp server, CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            var client = new McpClient(httpClient, server, _logger);

            var descriptors = await client.ListToolsAsync(cancellationToken).ConfigureAwait(false);

            // Apply tool whitelist if the descriptor declares one.
            // null = expose every tool; empty list = expose none.
            IEnumerable<McpToolDescriptor> visible = descriptors;
            if (server.AllowedTools is { } allow)
            {
                var allowSet = new HashSet<string>(allow, StringComparer.Ordinal);
                visible = descriptors.Where(d => allowSet.Contains(d.Name));
            }

            var tools = new List<ITool>();
            foreach (var d in visible)
                tools.Add(new McpTool(client, d, server.Name));

            _logger.LogInformation(
                "MCP [{Server}] exposed {Count} tool(s): {Tools}",
                server.Name,
                tools.Count,
                string.Join(", ", tools.Select(t => t.Name)));

            return tools;
        }
        catch (Exception ex)
        {
            // Don't fail the entire agent run if one MCP server is unreachable —
            // log and return nothing for this server.
            _logger.LogError(ex, "MCP [{Server}] failed to list tools — server will be ignored for this call.", server.Name);
            return Array.Empty<ITool>();
        }
    }

    /// <summary>
    /// Named HttpClient key used by <c>AddAiMcpClient()</c>.
    /// </summary>
    public const string HttpClientName = "Zonit.Extensions.Ai.Mcp";
}

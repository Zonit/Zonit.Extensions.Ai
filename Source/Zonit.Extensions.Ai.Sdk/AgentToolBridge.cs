using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Ai.Sdk;

/// <summary>
/// Default <see cref="IAgentToolBridge"/>: a process-lifetime singleton owning one shared
/// <see cref="LoopbackMcpServer"/>. Each <see cref="StartAsync"/> mints a per-session
/// bearer token, registers the tool set under it, and returns the loopback URL + token.
/// Disposing the singleton (on container shutdown) stops the listener.
/// </summary>
internal sealed class AgentToolBridge : IAgentToolBridge, IAsyncDisposable, IDisposable
{
    /// <summary>Logical MCP server name — the CLI exposes tools as <c>mcp__zonit__&lt;tool&gt;</c>.</summary>
    private const string ServerName = "zonit";

    private readonly LoopbackMcpServer _server;

    public AgentToolBridge(ILogger<AgentToolBridge> logger) => _server = new LoopbackMcpServer(logger);

    /// <inheritdoc />
    public Task<IAgentToolBridgeSession> StartAsync(IReadOnlyList<ITool> tools, CancellationToken cancellationToken)
    {
        var url = _server.EnsureStarted();
        var token = GenerateToken();
        _server.Register(token, tools);

        var toolNames = new string[tools.Count];
        for (var i = 0; i < tools.Count; i++) toolNames[i] = tools[i].Name;

        IAgentToolBridgeSession session = new BridgeSession(_server, token, url, ServerName, toolNames);
        return Task.FromResult(session);
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexStringLower(bytes);
    }

    public ValueTask DisposeAsync() => _server.DisposeAsync();

    public void Dispose() => _server.Dispose();

    private sealed class BridgeSession : IAgentToolBridgeSession
    {
        private readonly LoopbackMcpServer _server;
        private readonly string _token;

        public BridgeSession(LoopbackMcpServer server, string token, Uri url, string serverName, IReadOnlyList<string> toolNames)
        {
            _server = server;
            _token = token;
            Url = url;
            ServerName = serverName;
            ToolNames = toolNames;
            AuthToken = token;
        }

        public string ServerName { get; }
        public Uri Url { get; }
        public string? AuthToken { get; }
        public IReadOnlyList<string> ToolNames { get; }

        public ValueTask DisposeAsync()
        {
            _server.Unregister(_token);
            return ValueTask.CompletedTask;
        }
    }
}

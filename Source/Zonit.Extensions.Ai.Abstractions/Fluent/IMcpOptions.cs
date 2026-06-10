namespace Zonit.Extensions.Ai;

/// <summary>
/// Optional per-server configuration passed to
/// <c>AddMcp(name, url, token, configure)</c> on a fluent request builder.
/// Kept separate from the request's own knobs so MCP wiring never mixes with
/// tool / iteration / timeout configuration on the same call chain.
/// </summary>
public interface IMcpOptions
{
    /// <summary>
    /// Whitelist of remote tool names to expose from this server (without the
    /// <c>"{name}."</c> prefix). When unset, all of the server's tools are
    /// exposed. Maps to <see cref="Mcp.AllowedTools"/>.
    /// </summary>
    IMcpOptions AllowOnly(params string[] toolNames);
}

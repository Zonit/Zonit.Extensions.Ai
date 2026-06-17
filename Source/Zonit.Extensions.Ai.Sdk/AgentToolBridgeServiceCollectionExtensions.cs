using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Sdk;

namespace Zonit.Extensions;

/// <summary>
/// DI registration for the SDK tool bridge (<c>Zonit.Extensions.Ai.Sdk</c>).
/// </summary>
public static class AgentToolBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the loopback MCP tool bridge (<see cref="IAgentToolBridge"/>) so that
    /// agents running through the Claude Code CLI transport can call this app's
    /// <see cref="ITool"/> set. Required for tool-using agents on the <c>Sdk</c>/<c>Auto</c>
    /// Anthropic transport; without it those agents either throw (<c>Sdk</c>) or fall back
    /// to the HTTP API (<c>Auto</c>, when an API key is configured).
    /// </summary>
    /// <remarks>
    /// The bridge hosts a single loopback (<c>127.0.0.1</c>) MCP server for the process
    /// lifetime, secured with a per-agent-run bearer token. Idempotent.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAgentToolBridge(this IServiceCollection services)
    {
        services.TryAddSingleton<IAgentToolBridge, AgentToolBridge>();
        return services;
    }
}

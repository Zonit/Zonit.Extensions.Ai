using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for agent components: custom tools
/// (<see cref="ITool"/> / <c>ToolBase&lt;,&gt;</c>) and external MCP servers.
/// </summary>
/// <remarks>
/// <para>
/// Tools registered through these methods become <b>DI defaults</b>: they
/// are exposed to the model only when the caller invokes the agent with
/// <c>tools: null</c> (the "I have no opinion" signal). Passing an explicit
/// <c>tools:</c> list — including an empty one — is authoritative and
/// shadows DI defaults entirely. To suppress DI defaults even when no
/// per-call list was supplied, set <see cref="AgentOptions.DefaultTools"/>
/// (or <see cref="AgentOptions.DefaultMcp"/>) to <c>false</c>.
/// </para>
/// <para>
/// Defaults should stay small: globally useful capabilities like
/// <c>report_bug</c>, <c>search_internet</c>, <c>save_log</c>. Inheriting
/// from <c>ToolBase&lt;,&gt;</c> alone does <i>not</i> enrol a tool here —
/// auto-discovery only makes the concrete type DI-resolvable so callers can
/// pass it per-invocation (<c>tools: [sp.GetRequiredService&lt;MyTool&gt;()]</c>).
/// Explicit <c>AddAiTools&lt;T&gt;()</c> is the only path that activates a
/// tool for every <c>tools: null</c> call.
/// </para>
/// </remarks>
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Registers a custom agent tool type in the DI container as a default
    /// available to every agent call (unless suppressed per-call via
    /// <see cref="AgentOptions.DefaultTools"/>). Idempotent.
    /// </summary>
    /// <typeparam name="TTool">A concrete <see cref="ITool"/> implementation.</typeparam>
    public static IServiceCollection AddAiTools<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTool>(
        this IServiceCollection services)
        where TTool : class, ITool
    {
        services.TryAddScoped<IToolRegistry, ToolRegistry>();
        services.TryAddScoped<TTool>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITool, TTool>());
        return services;
    }

    /// <summary>
    /// Registers a custom agent tool instance in the DI container as a default.
    /// Useful for stateless or pre-built tool objects (e.g. tools constructed
    /// from configuration without DI dependencies).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tool">A pre-built tool instance.</param>
    public static IServiceCollection AddAiTools(this IServiceCollection services, ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        services.TryAddScoped<IToolRegistry, ToolRegistry>();
        services.AddSingleton(tool);
        return services;
    }

    /// <summary>
    /// Registers a custom agent tool with a factory delegate — useful for
    /// tools built dynamically at runtime (plugins, configuration-driven tools).
    /// </summary>
    public static IServiceCollection AddAiTools<TTool>(
        this IServiceCollection services,
        Func<IServiceProvider, TTool> factory)
        where TTool : class, ITool
    {
        // No DAM needed: TTool is constructed by the user-provided factory, not by DI reflection.
        services.TryAddScoped<IToolRegistry, ToolRegistry>();
        services.Add(ServiceDescriptor.Scoped(typeof(TTool), sp => factory(sp)));
        services.Add(ServiceDescriptor.Scoped<ITool>(sp => sp.GetRequiredService<TTool>()));
        return services;
    }

    /// <summary>
    /// Backwards-compatible alias for <see cref="AddAiTools{TTool}(IServiceCollection)"/>.
    /// </summary>
    public static IServiceCollection AddAiTool<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTool>(
        this IServiceCollection services)
        where TTool : class, ITool
        => services.AddAiTools<TTool>();

    /// <summary>
    /// Backwards-compatible alias for <see cref="AddAiTools{TTool}(IServiceCollection, Func{IServiceProvider, TTool})"/>.
    /// </summary>
    public static IServiceCollection AddAiTool<TTool>(
        this IServiceCollection services,
        Func<IServiceProvider, TTool> factory)
        where TTool : class, ITool
        => services.AddAiTools(factory);

    /// <summary>
    /// Registers a declarative sub-agent (<see cref="IAgent"/>) in the DI container so it can be
    /// exposed to a parent run via <c>IAgentRequest.AddAgent&lt;T&gt;()</c> / <c>IChatRequest.AddAgent&lt;T&gt;()</c>.
    /// Idempotent. Register the sub-agent's own tools separately with <see cref="AddAiTools{TTool}(IServiceCollection)"/>
    /// so they are resolvable when it runs.
    /// </summary>
    /// <typeparam name="TAgent">A concrete <see cref="IAgent"/> implementation (typically an <c>AgentBase&lt;...&gt;</c> subclass).</typeparam>
    public static IServiceCollection AddAiAgent<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAgent>(
        this IServiceCollection services)
        where TAgent : class, IAgent
    {
        services.TryAddScoped<TAgent>();
        return services;
    }

    /// <summary>
    /// Registers an external MCP server as a default — every agent call sees
    /// it unless suppressed via <see cref="AgentOptions.DefaultMcp"/>.
    /// Duplicate <see cref="Mcp.Name"/> values throw when <see cref="IMcpRegistry"/>
    /// is first resolved.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="mcp">The MCP descriptor.</param>
    public static IServiceCollection AddAiMcp(this IServiceCollection services, Mcp mcp)
    {
        ArgumentNullException.ThrowIfNull(mcp);
        services.AddSingleton(mcp);
        return services;
    }
}

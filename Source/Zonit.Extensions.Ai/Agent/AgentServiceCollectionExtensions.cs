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
/// Tools registered through these methods become <b>defaults</b> — they are
/// added to every agent <c>GenerateAsync</c> call. Per-call tools/mcps are
/// merged on top (additive, never replacing). To opt out of defaults for a
/// single invocation use <see cref="AgentOptions.DefaultTools"/> /
/// <see cref="AgentOptions.DefaultMcp"/>.
/// </para>
/// <para>
/// Defaults should stay small: globally useful capabilities like
/// <c>report_bug</c>, <c>search_internet</c>, <c>save_log</c>. Don't register
/// every tool in the project here — that pollutes the DI surface for callers
/// that only need a subset.
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
    public static IServiceCollection AddAiTools<TTool>(this IServiceCollection services)
        where TTool : class, ITool
    {
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
        services.Add(ServiceDescriptor.Scoped(typeof(TTool), sp => factory(sp)));
        services.Add(ServiceDescriptor.Scoped<ITool>(sp => sp.GetRequiredService<TTool>()));
        return services;
    }

    /// <summary>
    /// Backwards-compatible alias for <see cref="AddAiTools{TTool}(IServiceCollection)"/>.
    /// </summary>
    public static IServiceCollection AddAiTool<TTool>(this IServiceCollection services)
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

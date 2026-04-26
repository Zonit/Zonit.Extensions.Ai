using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Ai;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for AI services.
/// </summary>
/// <remarks>
/// Provides extension methods for registering AI services in the dependency injection container.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // Register core AI services only
/// services.AddAi();
/// 
/// // With custom configuration
/// services.AddAi(options => options.Resilience.MaxRetryAttempts = 5);
/// </code>
/// </para>
/// </remarks>
public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Registers core AI services with optional configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is idempotent - it uses <c>TryAdd</c> internally to prevent duplicate registrations.
    /// Can be safely called multiple times from different modules or plugins.
    /// </para>
    /// <para>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai"</c>.
    /// The <paramref name="options"/> action is applied after configuration binding via <c>PostConfigure</c>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for global AI options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddAi(options =>
    /// {
    ///     options.Resilience.MaxRetryAttempts = 5;
    ///     options.Resilience.HttpClientTimeout = TimeSpan.FromMinutes(10);
    /// });
    /// </code>
    /// </example>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification =
            "AgentRunner and McpToolFactory rely on System.Text.Json reflection only as a fallback when " +
            "no AOT binding is registered for a response type. When consumers compile with the " +
            "Zonit.Extensions.Ai source generators (enabled by default via this package), bindings " +
            "are emitted statically and the reflection path is never taken.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification =
            "AgentRunner/McpToolFactory ctors are flagged [RequiresDynamicCode] because they fall back " +
            "to reflection-based JSON serialization when no AOT binding is available. In the supported " +
            "configuration (source generators on, which is the package default) every response/tool " +
            "payload type has a statically emitted JsonTypeInfo, so no runtime code generation is " +
            "needed and the RDC path is never reached.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification =
            "ToolDiscovery.RegisteredTypes is populated exclusively by ToolDiscoveryGenerator, " +
            "which emits a [DynamicDependency] for the parameterless constructor of every " +
            "registered tool type. Those constructors are therefore preserved under trimming.")]
    public static IServiceCollection AddAi(
        this IServiceCollection services,
        Action<AiOptions>? options = null)
    {
        // Register core AI provider (TryAdd prevents duplicates)
        services.TryAddSingleton<IAiProvider, AiProvider>();

        // Agent runtime — runner + always-empty registries so the runner can
        // resolve them even if the application never registers any tools/MCP.
        services.TryAddSingleton<AgentRunner>();
        services.TryAddScoped<IToolRegistry, ToolRegistry>();
        services.TryAddSingleton<IMcpRegistry, McpRegistry>();

        // MCP HTTP/SSE client — built into core. Named HttpClient + factory.
        services.AddHttpClient(McpToolFactory.HttpClientName);
        services.TryAddSingleton<IMcpToolFactory, McpToolFactory>();

        // Auto-register every ToolBase<,> the source generator announced via
        // ToolDiscovery (one [ModuleInitializer] per consumer assembly).
        // This replaces the old explicit AddAiTools() call.
        foreach (var toolType in ToolDiscovery.RegisteredTypes)
        {
            services.TryAddScoped(toolType);
            services.TryAddEnumerable(
                ServiceDescriptor.Scoped(typeof(ITool), toolType));
        }

        // Bind configuration from appsettings.json (AOT-safe via DAM-constrained helper).
        services.AddAiOptionsFromConfiguration<AiOptions>(AiOptions.SectionName);

        // Apply additional configuration via PostConfigure
        if (options is not null)
            services.PostConfigure(options);

        // NOTE: Auto-discovery of providers was REMOVED because it registered providers
        // without properly configured HttpClient (typed client with resilience handlers).
        // Providers must be registered explicitly via their extension methods:
        // - services.AddAiOpenAi()
        // - services.AddAiAnthropic()
        // - services.AddAiGoogle()
        // - etc.
        // This ensures HttpClient is configured with proper timeout (40+ min for AI)
        // and resilience policies (retry, circuit breaker).

        return services;
    }

    /// <summary>
    /// Checks if a specific model provider type is already registered.
    /// </summary>
    /// <remarks>
    /// This method checks for both direct registrations and keyed service registrations.
    /// It enables idempotent provider registration across multiple plugins.
    /// </remarks>
    /// <typeparam name="TProvider">The provider implementation type to check.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>True if the provider is already registered; otherwise, false.</returns>
    public static bool IsProviderRegistered<TProvider>(this IServiceCollection services)
        where TProvider : class, IModelProvider
    {
        // Check if TProvider is already registered as IModelProvider
        // This handles both direct registrations and keyed service registrations
        return services.Any(sd =>
            sd.ServiceType == typeof(IModelProvider) &&
            (sd.ImplementationType == typeof(TProvider) ||
             sd.ServiceKey is Type keyType && keyType == typeof(TProvider)));
    }

    /// <summary>
    /// Registers a model provider with factory delegate, ensuring idempotent registration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is safe to call multiple times from different plugins.
    /// It checks if the provider is already registered before adding.
    /// </para>
    /// <para>
    /// Uses keyed services to track provider types, enabling detection of factory-based registrations
    /// for idempotent behavior.
    /// </para>
    /// </remarks>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection TryAddModelProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
        this IServiceCollection services)
        where TProvider : class, IModelProvider
    {
        // Skip if already registered (idempotent)
        if (services.IsProviderRegistered<TProvider>())
            return services;

        // Register using keyed service to track the provider type
        // This allows IsProviderRegistered to detect factory-based registrations
        services.Add(ServiceDescriptor.KeyedTransient<IModelProvider, TProvider>(typeof(TProvider)));

        // Also register for IEnumerable<IModelProvider> using factory delegate
        // The factory resolves through container to get the typed HttpClient
        services.AddTransient<IModelProvider>(sp => sp.GetRequiredService<TProvider>());

        return services;
    }

    /// <summary>
    /// Binds an <c>Ai</c>-family options class (<see cref="AiOptions"/>, <c>OpenAiOptions</c>,
    /// <c>AnthropicOptions</c>, …) to an <c>appsettings.json</c> section. This is the
    /// AOT/trim-safe replacement for calling <c>AddOptions&lt;T&gt;().BindConfiguration(...)</c>
    /// directly: <typeparamref name="TOptions"/> has a
    /// <see cref="DynamicallyAccessedMembersAttribute"/> constraint that preserves its
    /// <c>PublicProperties</c> and <c>PublicFields</c>, which is exactly what the configuration
    /// binder needs. The framework's own <c>IL2026</c>/<c>IL3050</c> warnings are suppressed
    /// here — they are false positives for plain POCO options types.
    /// </summary>
    /// <remarks>
    /// Provider packages should call this helper instead of binding configuration themselves
    /// so that the suppressions live in one well-justified place.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification =
            "TOptions is a plain POCO whose PublicProperties/PublicFields are preserved by the " +
            "DynamicallyAccessedMembers constraint on the type parameter; the configuration binder " +
            "only reads those members, so the RequiresUnreferencedCode warning from BindConfiguration " +
            "does not apply here.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification =
            "The configuration binder may use reflection at runtime, but only over the PublicProperties " +
            "and PublicFields of TOptions — all of which are preserved via the DAM constraint. No " +
            "generic type expansion or runtime code generation is required for POCO options classes.")]
    public static OptionsBuilder<TOptions> AddAiOptionsFromConfiguration<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)] TOptions>(
        this IServiceCollection services,
        string sectionName)
        where TOptions : class
    {
        return services.AddOptions<TOptions>().BindConfiguration(sectionName);
    }

    /// <summary>
    /// Registers a model provider manually.
    /// </summary>
    /// <remarks>
    /// Used internally by provider packages (e.g., <c>Zonit.Extensions.Ai.OpenAi</c>).
    /// Uses <c>TryAddEnumerable</c> to prevent duplicate registrations.
    /// </remarks>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    [Obsolete("Use TryAddModelProvider<TProvider> instead for proper idempotent registration with typed HttpClient.")]
    public static IServiceCollection AddAiProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
        this IServiceCollection services)
        where TProvider : class, IModelProvider
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModelProvider, TProvider>());
        return services;
    }
}

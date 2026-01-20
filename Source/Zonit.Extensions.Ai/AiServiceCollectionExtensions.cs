using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai;

/// <summary>
/// DI extensions for AI services.
/// </summary>
public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Adds AI services with auto-discovery of providers.
    /// This method is idempotent - uses TryAdd internally to prevent duplicates.
    /// Can be safely called multiple times from different plugins.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional configuration action for global options.</param>
    /// <returns>AI builder for fluent configuration.</returns>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static AiBuilder AddAi(
        this IServiceCollection services,
        Action<AiOptions>? configure = null)
    {
        // Register core services (TryAdd prevents duplicates)
        services.TryAddSingleton<IAiProvider, AiProvider>();

        // Configure global options from appsettings.json if available
        services.AddOptions<AiOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection(AiOptions.SectionName).Bind(options);
            });

        // Apply additional configuration if provided
        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Auto-discover and register providers from loaded assemblies
        DiscoverAndRegisterProviders(services);

        return new AiBuilder(services);
    }

    /// <summary>
    /// Adds AI services with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration section.</param>
    /// <returns>AI builder for fluent configuration.</returns>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static AiBuilder AddAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton<IAiProvider, AiProvider>();

        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        DiscoverAndRegisterProviders(services);

        return new AiBuilder(services);
    }

    /// <summary>
    /// Registers a model provider manually.
    /// Used by provider packages (Zonit.Extensions.Ai.OpenAi, etc.)
    /// Uses TryAddEnumerable to prevent duplicate registrations.
    /// </summary>
    /// <typeparam name="TProvider">Provider type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddAiProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
        this IServiceCollection services)
        where TProvider : class, IModelProvider
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModelProvider, TProvider>());
        return services;
    }

    [RequiresUnreferencedCode("Uses reflection to scan assemblies and types.")]
    private static void DiscoverAndRegisterProviders(IServiceCollection services)
    {
        // Get all loaded assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("System") != true)
            .ToList();

        // Find all types with [AiProvider] attribute
        foreach (var assembly in assemblies)
        {
            try
            {
                var providerTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract)
                    .Where(t => t.GetCustomAttribute<AiProviderAttribute>() != null)
                    .Where(t => typeof(IModelProvider).IsAssignableFrom(t));

                foreach (var type in providerTypes)
                {
                    // TryAddEnumerable prevents duplicates
                    services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IModelProvider), type));
                }
            }
            catch
            {
                // Skip assemblies that can't be scanned
            }
        }
    }
}

/// <summary>
/// Builder for fluent AI configuration.
/// </summary>
public sealed class AiBuilder
{
    /// <summary>
    /// The underlying service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    internal AiBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Configures OpenAI options.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>Builder for chaining.</returns>
    public AiBuilder WithOpenAi(Action<OpenAiOptions> configure)
    {
        Services.Configure<AiOptions>(options => configure(options.OpenAi));
        return this;
    }

    /// <summary>
    /// Configures Anthropic options.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>Builder for chaining.</returns>
    public AiBuilder WithAnthropic(Action<AnthropicOptions> configure)
    {
        Services.Configure<AiOptions>(options => configure(options.Anthropic));
        return this;
    }

    /// <summary>
    /// Configures Google options.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>Builder for chaining.</returns>
    public AiBuilder WithGoogle(Action<GoogleOptions> configure)
    {
        Services.Configure<AiOptions>(options => configure(options.Google));
        return this;
    }

    /// <summary>
    /// Configures X (Grok) options.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>Builder for chaining.</returns>
    public AiBuilder WithX(Action<XOptions> configure)
    {
        Services.Configure<AiOptions>(options => configure(options.X));
        return this;
    }

    /// <summary>
    /// Configures resilience options.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>Builder for chaining.</returns>
    public AiBuilder WithResilience(Action<ResilienceOptions> configure)
    {
        Services.Configure<AiOptions>(options => configure(options.Resilience));
        return this;
    }
}

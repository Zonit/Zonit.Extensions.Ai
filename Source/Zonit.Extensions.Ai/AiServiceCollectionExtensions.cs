using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddAi(
        this IServiceCollection services,
        Action<AiOptions>? options = null)
    {
        // Register core AI provider (TryAdd prevents duplicates)
        services.TryAddSingleton<IAiProvider, AiProvider>();

        // Bind configuration from appsettings.json
        services.AddOptions<AiOptions>()
            .BindConfiguration(AiOptions.SectionName);

        // Apply additional configuration via PostConfigure
        if (options is not null)
            services.PostConfigure(options);

        // Auto-discover and register providers from loaded assemblies
        DiscoverAndRegisterProviders(services);

        return services;
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
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("System") != true)
            .ToList();

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

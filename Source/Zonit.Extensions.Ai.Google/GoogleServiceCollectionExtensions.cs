using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// DI extensions for Google provider.
/// </summary>
public static class GoogleServiceCollectionExtensions
{
    /// <summary>
    /// Adds Google provider with API key.
    /// </summary>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddGoogle(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddGoogle(options => options.Google.ApiKey = apiKey);
    }

    /// <summary>
    /// Adds Google provider with configuration.
    /// </summary>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddGoogle(
        this IServiceCollection services,
        Action<AiOptions>? configure = null)
    {
        services.AddAi(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModelProvider, GoogleProvider>());

        services.AddHttpClient<GoogleProvider>()
            .AddStandardResilienceHandler();

        return services;
    }
}

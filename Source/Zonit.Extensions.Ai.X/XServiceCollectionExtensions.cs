using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// DI extensions for X provider.
/// </summary>
public static class XServiceCollectionExtensions
{
    /// <summary>
    /// Adds X provider with API key.
    /// </summary>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddX(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddX(options => options.X.ApiKey = apiKey);
    }
    
    /// <summary>
    /// Adds X provider with configuration.
    /// </summary>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddX(
        this IServiceCollection services,
        Action<AiOptions>? configure = null)
    {
        services.AddAi(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModelProvider, XProvider>());
        
        services.AddHttpClient<XProvider>()
            .AddStandardResilienceHandler();
        
        return services;
    }
}

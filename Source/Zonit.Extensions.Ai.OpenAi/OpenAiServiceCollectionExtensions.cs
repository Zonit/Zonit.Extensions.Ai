using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// DI extensions for OpenAI provider.
/// </summary>
public static class OpenAiServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenAI provider with API key.
    /// </summary>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddOpenAi(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddOpenAi(options => options.OpenAi.ApiKey = apiKey);
    }
    
    /// <summary>
    /// Adds OpenAI provider with configuration.
    /// </summary>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddOpenAi(
        this IServiceCollection services,
        Action<AiOptions>? configure = null)
    {
        // Ensure core AI services are registered
        services.AddAi(configure);
        
        // Register OpenAI provider
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModelProvider, OpenAiProvider>());
        
        // Register HttpClient with resilience
        services.AddHttpClient<OpenAiProvider>()
            .AddStandardResilienceHandler();
        
        return services;
    }
}

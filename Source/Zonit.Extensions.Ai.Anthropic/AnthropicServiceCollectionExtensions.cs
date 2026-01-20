using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// DI extensions for Anthropic provider.
/// </summary>
public static class AnthropicServiceCollectionExtensions
{
    /// <summary>
    /// Adds Anthropic provider with API key.
    /// </summary>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddAnthropic(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAnthropic(options => options.Anthropic.ApiKey = apiKey);
    }
    
    /// <summary>
    /// Adds Anthropic provider with configuration.
    /// </summary>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddAnthropic(
        this IServiceCollection services,
        Action<AiOptions>? configure = null)
    {
        services.AddAi(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModelProvider, AnthropicProvider>());
        
        services.AddHttpClient<AnthropicProvider>()
            .AddStandardResilienceHandler();
        
        return services;
    }
}

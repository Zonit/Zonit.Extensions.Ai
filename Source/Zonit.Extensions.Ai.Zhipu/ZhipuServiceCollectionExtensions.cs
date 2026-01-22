using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Zhipu;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Zhipu AI provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Zhipu AI as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiZhipu();
/// 
/// // With API key
/// services.AddAiZhipu("your-api-key");
/// 
/// // With custom configuration
/// services.AddAiZhipu(options =>
/// {
///     options.ApiKey = "...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class ZhipuServiceCollectionExtensions
{
    /// <summary>
    /// Registers Zhipu AI provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Zhipu API key.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddAiZhipu(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiZhipu(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Zhipu AI provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Zhipu options.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresUnreferencedCode("Auto-discovery of providers uses reflection to scan assemblies and types.")]
    public static IServiceCollection AddAiZhipu(
        this IServiceCollection services,
        Action<ZhipuOptions>? options = null)
    {
        services.AddAi();

        services.AddOptions<ZhipuOptions>()
            .BindConfiguration(ZhipuOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModelProvider, ZhipuProvider>());

        services.AddHttpClient<ZhipuProvider>()
            .AddAiResilienceHandler();

        return services;
    }
}

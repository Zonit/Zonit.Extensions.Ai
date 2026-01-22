using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Baidu;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Baidu AI (Qianfan) provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Baidu AI as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiBaidu();
/// 
/// // With API key
/// services.AddAiBaidu("your-api-key");
/// 
/// // With custom configuration
/// services.AddAiBaidu(options =>
/// {
///     options.ApiKey = "...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class BaiduServiceCollectionExtensions
{
    /// <summary>
    /// Registers Baidu AI provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Qianfan API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiBaidu(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiBaidu(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Baidu AI provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Baidu options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiBaidu(
        this IServiceCollection services,
        Action<BaiduOptions>? options = null)
    {
        if (services.IsProviderRegistered<BaiduProvider>())
            return services;

        services.AddAi();

        services.AddOptions<BaiduOptions>()
            .BindConfiguration(BaiduOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<BaiduProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<BaiduProvider>();

        return services;
    }
}

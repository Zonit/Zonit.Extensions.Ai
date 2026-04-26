using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Yi;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for 01.AI Yi provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering 01.AI Yi as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiYi();
/// 
/// // With API key
/// services.AddAiYi("your-api-key");
/// 
/// // With custom configuration
/// services.AddAiYi(options =>
/// {
///     options.ApiKey = "...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class YiServiceCollectionExtensions
{
    /// <summary>
    /// Registers 01.AI Yi provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">01.AI API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiYi(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiYi(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers 01.AI Yi provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Yi options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiYi(
        this IServiceCollection services,
        Action<YiOptions>? options = null)
    {
        if (services.IsProviderRegistered<YiProvider>())
            return services;

        services.AddAi();

        services.AddAiOptionsFromConfiguration<YiOptions>(YiOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<YiProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<YiProvider>();

        return services;
    }
}

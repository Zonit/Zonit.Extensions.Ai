using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Fireworks;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Fireworks AI provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Fireworks AI as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiFireworks();
/// 
/// // With API key
/// services.AddAiFireworks("fw-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiFireworks(options =>
/// {
///     options.ApiKey = "fw-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class FireworksServiceCollectionExtensions
{
    /// <summary>
    /// Registers Fireworks AI provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Fireworks API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiFireworks(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiFireworks(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Fireworks AI provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Fireworks options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiFireworks(
        this IServiceCollection services,
        Action<FireworksOptions>? options = null)
    {
        if (services.IsProviderRegistered<FireworksProvider>())
            return services;

        services.AddAi();

        services.AddOptions<FireworksOptions>()
            .BindConfiguration(FireworksOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<FireworksProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<FireworksProvider>();

        return services;
    }
}

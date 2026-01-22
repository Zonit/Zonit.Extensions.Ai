using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Together;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Together AI provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Together AI as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiTogether();
/// 
/// // With API key
/// services.AddAiTogether("your-api-key");
/// 
/// // With custom configuration
/// services.AddAiTogether(options =>
/// {
///     options.ApiKey = "...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class TogetherServiceCollectionExtensions
{
    /// <summary>
    /// Registers Together AI provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Together API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiTogether(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiTogether(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Together AI provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Together options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiTogether(
        this IServiceCollection services,
        Action<TogetherOptions>? options = null)
    {
        if (services.IsProviderRegistered<TogetherProvider>())
            return services;

        services.AddAi();

        services.AddOptions<TogetherOptions>()
            .BindConfiguration(TogetherOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<TogetherProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<TogetherProvider>();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Perplexity;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Perplexity AI provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Perplexity AI as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiPerplexity();
/// 
/// // With API key
/// services.AddAiPerplexity("pplx-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiPerplexity(options =>
/// {
///     options.ApiKey = "pplx-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class PerplexityServiceCollectionExtensions
{
    /// <summary>
    /// Registers Perplexity AI provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Perplexity API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiPerplexity(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiPerplexity(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Perplexity AI provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Perplexity options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiPerplexity(
        this IServiceCollection services,
        Action<PerplexityOptions>? options = null)
    {
        if (services.IsProviderRegistered<PerplexityProvider>())
            return services;

        services.AddAi();

        services.AddAiOptionsFromConfiguration<PerplexityOptions>(PerplexityOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<PerplexityProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<PerplexityProvider>();

        return services;
    }
}

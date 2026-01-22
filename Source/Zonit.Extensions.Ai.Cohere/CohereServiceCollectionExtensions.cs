using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Cohere;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Cohere provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Cohere as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiCohere();
/// 
/// // With API key
/// services.AddAiCohere("your-api-key");
/// 
/// // With custom configuration
/// services.AddAiCohere(options =>
/// {
///     options.ApiKey = "...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class CohereServiceCollectionExtensions
{
    /// <summary>
    /// Registers Cohere provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Cohere API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiCohere(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiCohere(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Cohere provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Cohere options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiCohere(
        this IServiceCollection services,
        Action<CohereOptions>? options = null)
    {
        services.AddAi();

        services.AddOptions<CohereOptions>()
            .BindConfiguration(CohereOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        // Register HttpClient with resilience optimized for AI (40min timeout, retry, circuit breaker)
        services.AddHttpClient<CohereProvider>()
            .AddAiResilienceHandler();

        // Register as IModelProvider using factory delegate to use typed HttpClient
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IModelProvider>(sp => sp.GetRequiredService<CohereProvider>()));

        return services;
    }
}

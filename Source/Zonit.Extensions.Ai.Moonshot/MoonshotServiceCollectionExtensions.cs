using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Moonshot;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Moonshot AI provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Moonshot AI (Kimi) as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiMoonshot();
/// 
/// // With API key
/// services.AddAiMoonshot("your-api-key");
/// 
/// // With custom configuration
/// services.AddAiMoonshot(options =>
/// {
///     options.ApiKey = "...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class MoonshotServiceCollectionExtensions
{
    /// <summary>
    /// Registers Moonshot AI provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Moonshot API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiMoonshot(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiMoonshot(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Moonshot AI provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Moonshot options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiMoonshot(
        this IServiceCollection services,
        Action<MoonshotOptions>? options = null)
    {
        services.AddAi();

        services.AddOptions<MoonshotOptions>()
            .BindConfiguration(MoonshotOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        // Register HttpClient with resilience optimized for AI (40min timeout, retry, circuit breaker)
        services.AddHttpClient<MoonshotProvider>()
            .AddAiResilienceHandler();

        // Register as IModelProvider using factory delegate to use typed HttpClient
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IModelProvider>(sp => sp.GetRequiredService<MoonshotProvider>()));

        return services;
    }
}

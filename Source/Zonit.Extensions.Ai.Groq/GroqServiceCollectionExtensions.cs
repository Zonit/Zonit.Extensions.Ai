using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Groq;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Groq provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Groq as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiGroq();
/// 
/// // With API key
/// services.AddAiGroq("gsk-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiGroq(options =>
/// {
///     options.ApiKey = "gsk-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class GroqServiceCollectionExtensions
{
    /// <summary>
    /// Registers Groq provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Groq API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiGroq(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiGroq(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Groq provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Groq options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiGroq(
        this IServiceCollection services,
        Action<GroqOptions>? options = null)
    {
        if (services.IsProviderRegistered<GroqProvider>())
            return services;

        services.AddAi();

        services.AddOptions<GroqOptions>()
            .BindConfiguration(GroqOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<GroqProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<GroqProvider>();

        return services;
    }
}

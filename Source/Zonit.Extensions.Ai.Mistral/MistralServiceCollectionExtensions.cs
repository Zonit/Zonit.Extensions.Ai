using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Mistral;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Mistral provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Mistral as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiMistral();
/// 
/// // With API key
/// services.AddAiMistral("your-api-key");
/// 
/// // With custom configuration
/// services.AddAiMistral(options =>
/// {
///     options.ApiKey = "...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class MistralServiceCollectionExtensions
{
    /// <summary>
    /// Registers Mistral provider with the specified API key.
    /// </summary>
    /// <remarks>
    /// Automatically registers core AI services if not already registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Mistral API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiMistral(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiMistral(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Mistral provider with optional configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai:Mistral"</c>.
    /// The <paramref name="options"/> action is applied after configuration binding via <c>PostConfigure</c>.
    /// </para>
    /// <para>
    /// Automatically registers core AI services if not already registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Mistral options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiMistral(
        this IServiceCollection services,
        Action<MistralOptions>? options = null)
    {
        // Ensure core AI services are registered
        services.AddAi();

        // Bind configuration from appsettings.json
        services.AddOptions<MistralOptions>()
            .BindConfiguration(MistralOptions.SectionName);

        // Apply additional configuration via PostConfigure
        if (options is not null)
            services.PostConfigure(options);

        // Register HttpClient with resilience optimized for AI (40min timeout, retry, circuit breaker)
        // AddHttpClient<T>() registers T as Transient with properly configured HttpClient.
        services.AddHttpClient<MistralProvider>()
            .AddAiResilienceHandler();

        // Register as IModelProvider using factory delegate.
        // This resolves MistralProvider through the container, which uses the typed HttpClient
        // registered by AddHttpClient<MistralProvider>() above.
        // TryAddEnumerable ensures idempotent registration for IEnumerable<IModelProvider>.
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IModelProvider>(sp => sp.GetRequiredService<MistralProvider>()));

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Google;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Google Gemini provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Google Gemini as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiGoogle();
/// 
/// // With API key
/// services.AddAiGoogle("AIza-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiGoogle(options =>
/// {
///     options.ApiKey = "AIza...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class GoogleServiceCollectionExtensions
{
    /// <summary>
    /// Registers Google Gemini provider with the specified API key.
    /// </summary>
    /// <remarks>
    /// Automatically registers core AI services if not already registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Google API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiGoogle(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiGoogle(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Google Gemini provider with optional configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai:Google"</c>.
    /// The <paramref name="options"/> action is applied after configuration binding via <c>PostConfigure</c>.
    /// </para>
    /// <para>
    /// Automatically registers core AI services if not already registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Google options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiGoogle(
        this IServiceCollection services,
        Action<GoogleOptions>? options = null)
    {
        // Ensure core AI services are registered
        services.AddAi();

        // Bind configuration from appsettings.json
        services.AddOptions<GoogleOptions>()
            .BindConfiguration(GoogleOptions.SectionName);

        // Apply additional configuration via PostConfigure
        if (options is not null)
            services.PostConfigure(options);

        // Register HttpClient with resilience optimized for AI (40min timeout, retry, circuit breaker)
        // AddHttpClient<T>() registers T as Transient with properly configured HttpClient.
        services.AddHttpClient<GoogleProvider>()
            .AddAiResilienceHandler();

        // Register as IModelProvider using factory delegate.
        // This resolves GoogleProvider through the container, which uses the typed HttpClient
        // registered by AddHttpClient<GoogleProvider>() above.
        // TryAddEnumerable ensures idempotent registration for IEnumerable<IModelProvider>.
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IModelProvider>(sp => sp.GetRequiredService<GoogleProvider>()));

        return services;
    }
}

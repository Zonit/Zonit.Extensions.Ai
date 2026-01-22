using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for X (Grok) AI provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering X/Grok as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiX();
/// 
/// // With API key
/// services.AddAiX("xai-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiX(options =>
/// {
///     options.ApiKey = "xai-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class XServiceCollectionExtensions
{
    /// <summary>
    /// Registers X (Grok) provider with the specified API key.
    /// </summary>
    /// <remarks>
    /// Automatically registers core AI services if not already registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">X API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiX(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiX(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers X (Grok) provider with optional configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai:X"</c>.
    /// The <paramref name="options"/> action is applied after configuration binding via <c>PostConfigure</c>.
    /// </para>
    /// <para>
    /// Automatically registers core AI services if not already registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for X options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiX(
        this IServiceCollection services,
        Action<XOptions>? options = null)
    {
        // Ensure core AI services are registered
        services.AddAi();

        // Bind configuration from appsettings.json
        services.AddOptions<XOptions>()
            .BindConfiguration(XOptions.SectionName);

        // Apply additional configuration via PostConfigure
        if (options is not null)
            services.PostConfigure(options);

        // Register HttpClient with resilience optimized for AI (40min timeout, retry, circuit breaker)
        // AddHttpClient<T>() registers T as Transient with properly configured HttpClient.
        services.AddHttpClient<XProvider>()
            .AddAiResilienceHandler();

        // Register as IModelProvider using factory delegate.
        // This resolves XProvider through the container, which uses the typed HttpClient
        // registered by AddHttpClient<XProvider>() above.
        // TryAddEnumerable ensures idempotent registration for IEnumerable<IModelProvider>.
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IModelProvider>(sp => sp.GetRequiredService<XProvider>()));

        return services;
    }
}

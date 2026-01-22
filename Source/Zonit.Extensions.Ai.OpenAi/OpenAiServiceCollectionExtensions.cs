using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.OpenAi;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for OpenAI provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering OpenAI as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiOpenAi();
/// 
/// // With API key
/// services.AddAiOpenAi("sk-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiOpenAi(options =>
/// {
///     options.ApiKey = "sk-...";
///     options.OrganizationId = "org-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class OpenAiServiceCollectionExtensions
{
    /// <summary>
    /// Registers OpenAI provider with the specified API key.
    /// </summary>
    /// <remarks>
    /// Automatically registers core AI services if not already registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiOpenAi(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiOpenAi(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers OpenAI provider with optional configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai:OpenAi"</c>.
    /// The <paramref name="options"/> action is applied after configuration binding via <c>PostConfigure</c>.
    /// </para>
    /// <para>
    /// Automatically registers core AI services if not already registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for OpenAI options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddAiOpenAi(options =>
    /// {
    ///     options.ApiKey = "sk-...";
    ///     options.OrganizationId = "org-...";
    ///     options.BaseUrl = "https://custom-endpoint.com";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAiOpenAi(
        this IServiceCollection services,
        Action<OpenAiOptions>? options = null)
    {
        // Ensure core AI services are registered
        services.AddAi();

        // Bind configuration from appsettings.json
        services.AddOptions<OpenAiOptions>()
            .BindConfiguration(OpenAiOptions.SectionName);

        // Apply additional configuration via PostConfigure
        if (options is not null)
            services.PostConfigure(options);

        // Register HttpClient with resilience optimized for AI (40min timeout, retry, circuit breaker)
        // AddHttpClient<T>() registers T as Transient with properly configured HttpClient.
        services.AddHttpClient<OpenAiProvider>()
            .AddAiResilienceHandler();

        // Register as IModelProvider using factory delegate.
        // This resolves OpenAiProvider through the container, which uses the typed HttpClient
        // registered by AddHttpClient<OpenAiProvider>() above.
        // TryAddEnumerable ensures idempotent registration for IEnumerable<IModelProvider>.
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IModelProvider>(sp => sp.GetRequiredService<OpenAiProvider>()));

        return services;
    }
}

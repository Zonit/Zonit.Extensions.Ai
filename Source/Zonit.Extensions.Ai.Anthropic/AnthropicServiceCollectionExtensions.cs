using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Anthropic Claude provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Anthropic as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiAnthropic();
/// 
/// // With API key
/// services.AddAiAnthropic("sk-ant-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiAnthropic(options =>
/// {
///     options.ApiKey = "sk-ant-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class AnthropicServiceCollectionExtensions
{
    /// <summary>
    /// Registers Anthropic provider with the specified API key.
    /// </summary>
    /// <remarks>
    /// Automatically registers core AI services if not already registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Anthropic API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAnthropic(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiAnthropic(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Anthropic provider with optional configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai:Anthropic"</c>.
    /// The <paramref name="options"/> action is applied after configuration binding via <c>PostConfigure</c>.
    /// </para>
    /// <para>
    /// Automatically registers core AI services if not already registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Anthropic options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAnthropic(
        this IServiceCollection services,
        Action<AnthropicOptions>? options = null)
    {
        // Ensure core AI services are registered
        services.AddAi();

        // Bind configuration from appsettings.json
        services.AddOptions<AnthropicOptions>()
            .BindConfiguration(AnthropicOptions.SectionName);

        // Apply additional configuration via PostConfigure
        if (options is not null)
            services.PostConfigure(options);

        // Register HttpClient with resilience optimized for AI (40min timeout, retry, circuit breaker)
        // AddHttpClient<T>() registers T as Transient with properly configured HttpClient.
        services.AddHttpClient<AnthropicProvider>()
            .AddAiResilienceHandler();

        // Register as IModelProvider using factory delegate.
        // This resolves AnthropicProvider through the container, which uses the typed HttpClient
        // registered by AddHttpClient<AnthropicProvider>() above.
        // TryAddEnumerable ensures idempotent registration for IEnumerable<IModelProvider>.
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IModelProvider>(sp => sp.GetRequiredService<AnthropicProvider>()));

        return services;
    }
}

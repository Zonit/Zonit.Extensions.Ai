using Microsoft.Extensions.DependencyInjection;
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
        if (services.IsProviderRegistered<XProvider>())
            return services;

        services.AddAi();

        services.AddAiOptionsFromConfiguration<XOptions>(XOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<XProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<XProvider>();

        // Agent adapter — separate typed HttpClient with the same resilience policies.
        services.AddHttpClient<XAgentAdapter>()
            .AddAiResilienceHandler();
        services.AddTransient<IAgentProviderAdapter>(
            sp => sp.GetRequiredService<XAgentAdapter>());

        return services;
    }
}

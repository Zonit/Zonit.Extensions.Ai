using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.ElevenLabs;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for the ElevenLabs provider.
/// </summary>
/// <remarks>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration ("Ai:ElevenLabs")
/// services.AddAiElevenLabs();
///
/// // With API key
/// services.AddAiElevenLabs("sk_your-api-key");
///
/// // With custom configuration
/// services.AddAiElevenLabs(options =>
/// {
///     options.ApiKey = "sk_...";
/// });
/// </code>
/// </remarks>
public static class ElevenLabsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ElevenLabs provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">ElevenLabs API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiElevenLabs(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiElevenLabs(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers the ElevenLabs provider with optional configuration.
    /// </summary>
    /// <remarks>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai:ElevenLabs"</c>;
    /// the <paramref name="options"/> action is applied afterwards via <c>PostConfigure</c>.
    /// Automatically registers core AI services if not already registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for ElevenLabs options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiElevenLabs(
        this IServiceCollection services,
        Action<ElevenLabsOptions>? options = null)
    {
        if (services.IsProviderRegistered<ElevenLabsProvider>())
            return services;

        services.AddAi();

        services.AddAiOptionsFromConfiguration<ElevenLabsOptions>(ElevenLabsOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<ElevenLabsProvider>()
            .AddAiResilienceHandler<ElevenLabsOptions>();

        services.TryAddModelProvider<ElevenLabsProvider>();

        return services;
    }
}

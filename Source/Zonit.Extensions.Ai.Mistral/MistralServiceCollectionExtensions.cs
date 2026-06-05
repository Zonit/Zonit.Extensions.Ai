using Microsoft.Extensions.DependencyInjection;
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
        if (services.IsProviderRegistered<MistralProvider>())
            return services;

        services.AddAi();

        services.AddAiOptionsFromConfiguration<MistralOptions>(MistralOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<MistralProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<MistralProvider>();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.DeepSeek;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for DeepSeek provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering DeepSeek as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiDeepSeek();
/// 
/// // With API key
/// services.AddAiDeepSeek("sk-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiDeepSeek(options =>
/// {
///     options.ApiKey = "sk-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class DeepSeekServiceCollectionExtensions
{
    /// <summary>
    /// Registers DeepSeek provider with the specified API key.
    /// </summary>
    /// <remarks>
    /// Automatically registers core AI services if not already registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">DeepSeek API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiDeepSeek(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiDeepSeek(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers DeepSeek provider with optional configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai:DeepSeek"</c>.
    /// The <paramref name="options"/> action is applied after configuration binding via <c>PostConfigure</c>.
    /// </para>
    /// <para>
    /// Automatically registers core AI services if not already registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for DeepSeek options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiDeepSeek(
        this IServiceCollection services,
        Action<DeepSeekOptions>? options = null)
    {
        if (services.IsProviderRegistered<DeepSeekProvider>())
            return services;

        services.AddAi();

        services.AddOptions<DeepSeekOptions>()
            .BindConfiguration(DeepSeekOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<DeepSeekProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<DeepSeekProvider>();

        return services;
    }
}

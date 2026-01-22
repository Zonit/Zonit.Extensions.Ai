using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Alibaba;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Alibaba Cloud (DashScope) provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Alibaba Cloud as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiAlibaba();
/// 
/// // With API key
/// services.AddAiAlibaba("sk-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiAlibaba(options =>
/// {
///     options.ApiKey = "sk-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class AlibabaServiceCollectionExtensions
{
    /// <summary>
    /// Registers Alibaba Cloud provider with the specified API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">DashScope API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAlibaba(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiAlibaba(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Alibaba Cloud provider with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Alibaba options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAlibaba(
        this IServiceCollection services,
        Action<AlibabaOptions>? options = null)
    {
        if (services.IsProviderRegistered<AlibabaProvider>())
            return services;

        services.AddAi();

        services.AddOptions<AlibabaOptions>()
            .BindConfiguration(AlibabaOptions.SectionName);

        if (options is not null)
            services.PostConfigure(options);

        services.AddHttpClient<AlibabaProvider>()
            .AddAiResilienceHandler();

        services.TryAddModelProvider<AlibabaProvider>();

        return services;
    }
}

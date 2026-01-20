using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Providers;
using Zonit.Extensions.Ai.Providers.Anthropic;
using Zonit.Extensions.Ai.Providers.Google;
using Zonit.Extensions.Ai.Providers.OpenAi;
using Zonit.Extensions.Ai.Providers.X;

namespace Zonit.Extensions;

/// <summary>
/// Extension methods for setting up AI services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AI services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure the <see cref="AiOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddAi(
        this IServiceCollection services,
        Action<AiOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AiOptions();
        configure(options);

        services.AddSingleton(options);

        // Register all providers
        ConfigureHttpClient<OpenAiProvider>(services, options, "OpenAI");
        ConfigureHttpClient<AnthropicProvider>(services, options, "Anthropic");
        ConfigureHttpClient<GoogleProvider>(services, options, "Google");
        ConfigureHttpClient<XProvider>(services, options, "X");

        // Register provider collection
        services.TryAddSingleton<IEnumerable<IModelProvider>>(sp => new IModelProvider[]
        {
            sp.GetRequiredService<OpenAiProvider>(),
            sp.GetRequiredService<AnthropicProvider>(),
            sp.GetRequiredService<GoogleProvider>(),
            sp.GetRequiredService<XProvider>()
        });

        // Register main AI provider
        services.TryAddSingleton<IAiProvider, AiProvider>();

        // Legacy compatibility
        services.TryAddSingleton<Zonit.Extensions.Ai.Abstractions.Legacy.IAiClient>(sp =>
            new Zonit.Extensions.Ai.Legacy.AiClientWrapper(sp.GetRequiredService<IAiProvider>()));

        return services;
    }

    /// <summary>
    /// [Obsolete] Use AddAi instead.
    /// </summary>
    [Obsolete("Use AddAi instead. Will be removed in v3.0.")]
    public static IServiceCollection AddAiExtension(this IServiceCollection services)
    {
        // Legacy - requires environment variable or app settings
        var apiKey = Environment.GetEnvironmentVariable("AI_API_KEY") ?? string.Empty;
        return services.AddAi(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Adds AI services with OpenAI as the default provider.
    /// </summary>
    public static IServiceCollection AddOpenAi(
        this IServiceCollection services,
        string apiKey,
        Action<AiOptions>? configure = null)
    {
        return services.AddAi(options =>
        {
            options.ApiKey = apiKey;
            options.DefaultProvider = "openai";
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds AI services with Anthropic as the default provider.
    /// </summary>
    public static IServiceCollection AddAnthropic(
        this IServiceCollection services,
        string apiKey,
        Action<AiOptions>? configure = null)
    {
        return services.AddAi(options =>
        {
            options.ApiKey = apiKey;
            options.DefaultProvider = "anthropic";
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds AI services with Google as the default provider.
    /// </summary>
    public static IServiceCollection AddGoogle(
        this IServiceCollection services,
        string apiKey,
        Action<AiOptions>? configure = null)
    {
        return services.AddAi(options =>
        {
            options.ApiKey = apiKey;
            options.DefaultProvider = "google";
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds AI services with X (Grok) as the default provider.
    /// </summary>
    public static IServiceCollection AddX(
        this IServiceCollection services,
        string apiKey,
        Action<AiOptions>? configure = null)
    {
        return services.AddAi(options =>
        {
            options.ApiKey = apiKey;
            options.DefaultProvider = "x";
            configure?.Invoke(options);
        });
    }

    private static void ConfigureHttpClient<TProvider>(
        IServiceCollection services,
        AiOptions options,
        string name)
        where TProvider : class, IModelProvider
    {
        var builder = services.AddHttpClient<TProvider>(name, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(options.Resilience.TimeoutMinutes);
        });

        // Configure resilience with Polly
        if (options.Resilience.Enabled)
        {
            builder.AddResilienceHandler($"{name}Resilience", pipeline =>
            {
                // Retry policy
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.Resilience.MaxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(2)
                });

                // Timeout per attempt
                pipeline.AddTimeout(TimeSpan.FromMinutes(options.Resilience.TimeoutMinutes));
            });
        }
    }
}
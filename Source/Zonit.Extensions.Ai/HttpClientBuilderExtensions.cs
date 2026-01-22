using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions;

/// <summary>
/// Extension methods for configuring resilience on HttpClient builders for AI providers.
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Adds resilience handling optimized for AI providers with long-running requests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configures retry, circuit breaker, and timeout policies optimized for AI API calls:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>Total Timeout:</b> 40 minutes (AI requests can be slow)</item>
    ///   <item><b>Per-Attempt Timeout:</b> 10 minutes</item>
    ///   <item><b>Retry:</b> 3 attempts with exponential backoff (2s base, 30s max)</item>
    ///   <item><b>Circuit Breaker:</b> Opens after 50% failure rate, 30s break</item>
    /// </list>
    /// <para>
    /// Configuration can be overridden via <c>appsettings.json</c> section <c>"Ai:Resilience"</c>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The HttpClient builder.</param>
    /// <returns>The HttpClient builder for chaining.</returns>
    public static IHttpClientBuilder AddAiResilienceHandler(this IHttpClientBuilder builder)
    {
        // Configure HttpClient timeout to be higher than total pipeline timeout
        // to let the resilience handler manage timeouts
        builder.ConfigureHttpClient((sp, client) =>
        {
            var options = sp.GetService<IOptions<AiOptions>>()?.Value ?? new AiOptions();
            // Set HttpClient.Timeout higher than total timeout to prevent premature cancellation
            client.Timeout = options.Resilience.TotalRequestTimeout + TimeSpan.FromMinutes(5);
        });

        // Use standard resilience handler and configure from options
        builder.AddStandardResilienceHandler()
            .Configure(ConfigureResilienceFromOptions);

        return builder;
    }

    /// <summary>
    /// Configures resilience settings from AiOptions.
    /// </summary>
    private static void ConfigureResilienceFromOptions(
        HttpStandardResilienceOptions options,
        IServiceProvider serviceProvider)
    {
        var aiOptions = serviceProvider.GetService<IOptions<AiOptions>>()?.Value ?? new AiOptions();
        var resilience = aiOptions.Resilience;

        // Configure timeouts
        options.TotalRequestTimeout.Timeout = resilience.TotalRequestTimeout;
        options.AttemptTimeout.Timeout = resilience.AttemptTimeout;

        // Configure retry policy with exponential backoff and jitter
        options.Retry.MaxRetryAttempts = resilience.MaxRetryAttempts;
        options.Retry.Delay = resilience.RetryBaseDelay;
        options.Retry.MaxDelay = resilience.RetryMaxDelay;
        options.Retry.UseJitter = resilience.UseJitter;

        // Configure circuit breaker
        options.CircuitBreaker.FailureRatio = resilience.CircuitBreakerFailureRatio;
        options.CircuitBreaker.MinimumThroughput = resilience.CircuitBreakerMinimumThroughput;
        options.CircuitBreaker.SamplingDuration = resilience.CircuitBreakerSamplingDuration;
        options.CircuitBreaker.BreakDuration = resilience.CircuitBreakerBreakDuration;
    }
}

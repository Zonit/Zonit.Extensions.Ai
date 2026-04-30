using System.Net.Http;
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

        // Tune the underlying socket handler. Without this AI clients sitting
        // idle behind a CDN/proxy (Cloudflare, corp gateway, Anthropic edge)
        // routinely pick a "live" pooled connection that the remote already
        // half-closed, then block on InitialFillAsync until the resilience
        // timeout fires — even though the request has only been idle for
        // ~30 seconds.
        builder.ConfigurePrimaryHttpMessageHandler(_ => CreateAiSocketsHandler());

        // Use standard resilience handler and configure from options
        builder.AddStandardResilienceHandler()
            .Configure(ConfigureResilienceFromOptions);

        return builder;
    }

    /// <summary>
    /// Builds a <see cref="SocketsHttpHandler"/> tuned for long-running AI traffic:
    /// short idle timeout to evict half-dead pooled connections, periodic HTTP/2
    /// PING frames so an in-flight streaming response never sits silent long
    /// enough for an intermediary to drop it, and multi-connection fallback so
    /// a single stuck stream cannot starve subsequent requests.
    /// </summary>
    private static SocketsHttpHandler CreateAiSocketsHandler() => new()
    {
        // Recycle every connection after 5 min so DNS / TLS / load-balancer
        // changes propagate and very-long-lived sockets don't accumulate
        // server-side state we cannot see.
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),

        // Default is 1 min, but typical edge proxies (Cloudflare ~100s, many
        // corp NATs ~60s) drop idle connections silently. Closing them on the
        // client side first means we open a fresh socket on the next request
        // instead of writing into a black hole.
        PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),

        // HTTP/2 keepalive: send a PING frame every 30 s while a request is
        // in flight, fail fast if no pong comes back within 15 s. This is the
        // mechanism that turns "10-minute hang then timeout" into "we notice
        // the dead connection immediately and the resilience handler retries".
        KeepAlivePingDelay = TimeSpan.FromSeconds(30),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(15),
        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,

        // Allow opening additional HTTP/2 connections if the existing one is
        // saturated or stalling — by default a single connection is multiplexed
        // and a stuck stream blocks unrelated traffic.
        EnableMultipleHttp2Connections = true,
    };

    /// <summary>
    /// Configures resilience settings from AiOptions.
    /// </summary>
    private static void ConfigureResilienceFromOptions(
        HttpStandardResilienceOptions options,
        IServiceProvider serviceProvider)
    {
        var aiOptions = serviceProvider.GetService<IOptions<AiOptions>>()?.Value ?? new AiOptions();
        var resilience = aiOptions.Resilience;

        // Validate and auto-correct configuration
        resilience.EnsureValid();

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

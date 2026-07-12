using System.Diagnostics.CodeAnalysis;
using System.Net;
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
        => AddAiResilienceHandlerCore(builder, streaming: false, useProxyResolver: null);

    /// <summary>
    /// <inheritdoc cref="AddAiResilienceHandler(IHttpClientBuilder)"/>
    /// <para>
    /// This overload also reads the provider's <typeparamref name="TOptions"/> so that
    /// its <see cref="AiProviderOptions.UseProxy"/> flag can exclude the provider from the
    /// global <see cref="AiProxyOptions"/> proxy while every other provider keeps using it.
    /// </para>
    /// </summary>
    /// <typeparam name="TOptions">The provider's options type.</typeparam>
    public static IHttpClientBuilder AddAiResilienceHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(
        this IHttpClientBuilder builder)
        where TOptions : AiProviderOptions
        => AddAiResilienceHandlerCore(builder, streaming: false, useProxyResolver: UseProxyResolver<TOptions>);

    /// <summary>
    /// Adds resilience handling for <i>streaming</i> AI clients (SSE-based
    /// agent loops, <c>ChatStreamAsync</c>). Identical to
    /// <see cref="AddAiResilienceHandler"/> but the per-attempt timeout is
    /// widened to <see cref="AiResilienceOptions.EffectiveStreamingAttemptTimeout"/>
    /// — by default equal to <c>TotalRequestTimeout</c> — so Polly no
    /// longer cancels healthy long-running streams at the (small)
    /// non-streaming <c>AttemptTimeout</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stream liveness is enforced client-side: the agent's inter-event
    /// SSE watchdog (60 s) reads the <c>ping</c> events Anthropic emits
    /// every ~10–15 s during extended thinking, and the HTTP/2 keepalive
    /// PING frame (45 s) catches dead sockets. A per-attempt Polly cap on
    /// top is redundant and actively harmful — it cancels at exactly
    /// <c>AttemptTimeout</c> while the model is still legitimately working,
    /// surfacing as <c>OperationCanceledException</c> with the duration
    /// pinned to the timeout (e.g. exactly 600 s for the 10-minute default).
    /// </para>
    /// <para>
    /// Retry policy and circuit breaker remain active so transient 5xx /
    /// network errors still recover and a repeatedly failing provider
    /// trips the breaker.
    /// </para>
    /// </remarks>
    /// <param name="builder">The HttpClient builder.</param>
    /// <returns>The HttpClient builder for chaining.</returns>
    public static IHttpClientBuilder AddAiStreamingResilienceHandler(this IHttpClientBuilder builder)
        => AddAiResilienceHandlerCore(builder, streaming: true, useProxyResolver: null);

    /// <summary>
    /// <inheritdoc cref="AddAiStreamingResilienceHandler(IHttpClientBuilder)"/>
    /// <para>
    /// This overload also reads the provider's <typeparamref name="TOptions"/> so that its
    /// <see cref="AiProviderOptions.UseProxy"/> flag can exclude the provider from the
    /// global <see cref="AiProxyOptions"/> proxy.
    /// </para>
    /// </summary>
    /// <typeparam name="TOptions">The provider's options type.</typeparam>
    public static IHttpClientBuilder AddAiStreamingResilienceHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(
        this IHttpClientBuilder builder)
        where TOptions : AiProviderOptions
        => AddAiResilienceHandlerCore(builder, streaming: true, useProxyResolver: UseProxyResolver<TOptions>);

    private static bool UseProxyResolver<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(
        IServiceProvider serviceProvider)
        where TOptions : AiProviderOptions
        => serviceProvider.GetService<IOptions<TOptions>>()?.Value.UseProxy ?? true;

    private static IHttpClientBuilder AddAiResilienceHandlerCore(
        IHttpClientBuilder builder,
        bool streaming,
        Func<IServiceProvider, bool>? useProxyResolver)
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
        builder.ConfigurePrimaryHttpMessageHandler(sp => CreateAiSocketsHandler(sp, useProxyResolver));

        // Use standard resilience handler and configure from options
        if (streaming)
        {
            builder.AddStandardResilienceHandler()
                .Configure(ConfigureStreamingResilienceFromOptions);
        }
        else
        {
            builder.AddStandardResilienceHandler()
                .Configure(ConfigureResilienceFromOptions);
        }

        return builder;
    }

    /// <summary>
    /// Builds a <see cref="SocketsHttpHandler"/> tuned for long-running AI traffic:
    /// short idle timeout to evict half-dead pooled connections, periodic HTTP/2
    /// PING frames so an in-flight streaming response never sits silent long
    /// enough for an intermediary to drop it, and multi-connection fallback so
    /// a single stuck stream cannot starve subsequent requests.
    /// </summary>
    private static SocketsHttpHandler CreateAiSocketsHandler(
        IServiceProvider serviceProvider,
        Func<IServiceProvider, bool>? useProxyResolver)
    {
        var handler = new SocketsHttpHandler
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

        // Route through the global proxy (Ai:Proxy) unless this provider opted out
        // via AiProviderOptions.UseProxy. Used e.g. to reach a region-locked model
        // (Grok 4.5 is EU-blocked) through an allowed-region exit node.
        var useProxy = useProxyResolver?.Invoke(serviceProvider) ?? true;
        if (useProxy)
        {
            var proxy = ResolveProxy(serviceProvider.GetService<IOptions<AiOptions>>()?.Value.Proxy);
            if (proxy is not null)
            {
                handler.Proxy = proxy;
                handler.UseProxy = true;
            }
        }

        return handler;
    }

    /// <summary>
    /// Builds an <see cref="IWebProxy"/> from <see cref="AiProxyOptions"/>, or
    /// <c>null</c> when no proxy should apply (options null, <see cref="AiProxyOptions.Enabled"/>
    /// false, or no <see cref="AiProxyOptions.Address"/>). Supports HTTP and SOCKS
    /// addresses; attaches credentials when a username is configured. Internal for
    /// deterministic unit testing.
    /// </summary>
    internal static IWebProxy? ResolveProxy(AiProxyOptions? options)
    {
        if (options is null || !options.Enabled || string.IsNullOrWhiteSpace(options.Address))
            return null;

        var proxy = new WebProxy(options.Address);
        if (!string.IsNullOrEmpty(options.Username))
            proxy.Credentials = new NetworkCredential(options.Username, options.Password);

        return proxy;
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

    /// <summary>
    /// Configures resilience settings for streaming clients. Per-attempt
    /// timeout is widened to <see cref="AiResilienceOptions.EffectiveStreamingAttemptTimeout"/>
    /// (default = <c>TotalRequestTimeout</c>) so SSE streams that legitimately
    /// run for many minutes are no longer cancelled at the small non-streaming
    /// <c>AttemptTimeout</c>. CircuitBreaker.SamplingDuration is bumped to
    /// satisfy Polly's 2x AttemptTimeout constraint automatically.
    /// </summary>
    private static void ConfigureStreamingResilienceFromOptions(
        HttpStandardResilienceOptions options,
        IServiceProvider serviceProvider)
    {
        var aiOptions = serviceProvider.GetService<IOptions<AiOptions>>()?.Value ?? new AiOptions();
        var resilience = aiOptions.Resilience;

        resilience.EnsureValid();

        var streamingAttempt = resilience.EffectiveStreamingAttemptTimeout;

        options.TotalRequestTimeout.Timeout = resilience.TotalRequestTimeout;
        options.AttemptTimeout.Timeout = streamingAttempt;

        // Retry policy stays unchanged — transient 5xx / connection drops
        // still recover via Polly retries on streaming clients too.
        options.Retry.MaxRetryAttempts = resilience.MaxRetryAttempts;
        options.Retry.Delay = resilience.RetryBaseDelay;
        options.Retry.MaxDelay = resilience.RetryMaxDelay;
        options.Retry.UseJitter = resilience.UseJitter;

        // CircuitBreaker.SamplingDuration MUST be >= 2x AttemptTimeout
        // (Polly StandardResilienceHandler validation). With streaming
        // AttemptTimeout typically equal to TotalRequestTimeout (40 min
        // default), the regular 25-minute SamplingDuration fails validation.
        // Auto-correct defensively here using the same 2.5x safety margin
        // EnsureValid() uses for the non-streaming path.
        var minSampling = TimeSpan.FromTicks((long)(streamingAttempt.Ticks * 2.5));
        options.CircuitBreaker.FailureRatio = resilience.CircuitBreakerFailureRatio;
        options.CircuitBreaker.MinimumThroughput = resilience.CircuitBreakerMinimumThroughput;
        options.CircuitBreaker.SamplingDuration =
            resilience.CircuitBreakerSamplingDuration >= minSampling
                ? resilience.CircuitBreakerSamplingDuration
                : minSampling;
        options.CircuitBreaker.BreakDuration = resilience.CircuitBreakerBreakDuration;
    }
}

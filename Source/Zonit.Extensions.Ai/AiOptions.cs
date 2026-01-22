namespace Zonit.Extensions;

/// <summary>
/// Global AI configuration options.
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai"</c>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "Resilience": {
///       "MaxRetryAttempts": 3,
///       "RetryBaseDelay": "00:00:02"
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AiOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai";

    /// <summary>
    /// Resilience configuration for all providers (retry, timeout, circuit breaker).
    /// </summary>
    public AiResilienceOptions Resilience { get; set; } = new();
}

/// <summary>
/// Resilience configuration for HTTP requests to AI providers.
/// </summary>
/// <remarks>
/// Configures retry policies, timeouts, and circuit breaker behavior
/// for all AI provider HTTP calls. Optimized for long-running AI requests.
/// <para>
/// <b>Important:</b> CircuitBreakerSamplingDuration must be at least 2x AttemptTimeout
/// (Polly requirement). Default values are configured accordingly.
/// </para>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "Resilience": {
///       "TotalRequestTimeout": "00:40:00",
///       "AttemptTimeout": "00:10:00",
///       "MaxRetryAttempts": 3
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AiResilienceOptions
{
    /// <summary>
    /// Total timeout for the entire request pipeline, including all retry attempts.
    /// Default: 40 minutes.
    /// </summary>
    /// <remarks>
    /// This is the maximum time the request can take from start to finish,
    /// including all retries. AI requests can take many minutes, especially
    /// for complex prompts with reasoning models.
    /// </remarks>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromMinutes(40);

    /// <summary>
    /// Timeout for a single request attempt.
    /// Default: 10 minutes.
    /// </summary>
    /// <remarks>
    /// Each individual request attempt (before retry) will timeout after this duration.
    /// Should be less than <see cref="TotalRequestTimeout"/>.
    /// AI models can take 5-10 minutes for complex reasoning tasks.
    /// <para>
    /// <b>Note:</b> CircuitBreakerSamplingDuration must be at least 2x this value.
    /// </para>
    /// </remarks>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Maximum number of retry attempts before failing.
    /// Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retry attempts (exponential backoff).
    /// Default: 2 seconds.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum delay between retry attempts.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to add random jitter to retry delays to prevent thundering herd.
    /// Default: true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Duration of the sampling period for circuit breaker failure ratio calculation.
    /// Default: 25 minutes.
    /// </summary>
    /// <remarks>
    /// <b>Must be at least 2x <see cref="AttemptTimeout"/></b> (Polly requirement).
    /// Default is 25 minutes to accommodate the default 10-minute attempt timeout.
    /// </remarks>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromMinutes(25);

    /// <summary>
    /// Failure ratio threshold to open the circuit breaker.
    /// Default: 0.5 (50% failures).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum number of requests in sampling period before circuit breaker can open.
    /// Default: 5.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>
    /// Duration the circuit breaker stays open before allowing test requests.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// HTTP client timeout for requests.
    /// Default: 5 minutes.
    /// </summary>
    /// <remarks>
    /// Deprecated. Use <see cref="TotalRequestTimeout"/> and <see cref="AttemptTimeout"/> instead.
    /// </remarks>
    [Obsolete("Use TotalRequestTimeout and AttemptTimeout instead. This property is ignored.")]
    public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Validates the configuration and auto-corrects invalid values.
    /// </summary>
    /// <remarks>
    /// Ensures CircuitBreakerSamplingDuration is at least 2x AttemptTimeout.
    /// Called automatically during resilience handler configuration.
    /// </remarks>
    internal void EnsureValid()
    {
        var minSamplingDuration = AttemptTimeout * 2.5; // 2.5x for safety margin

        if (CircuitBreakerSamplingDuration < minSamplingDuration)
        {
            CircuitBreakerSamplingDuration = minSamplingDuration;
        }

        if (TotalRequestTimeout <= AttemptTimeout)
        {
            TotalRequestTimeout = AttemptTimeout * 3; // Allow for retries
        }
    }
}

/// <summary>
/// Base class for AI provider configuration options.
/// </summary>
/// <remarks>
/// All provider-specific options (OpenAI, Anthropic, etc.) inherit from this class.
/// </remarks>
public abstract class AiProviderOptions
{
    /// <summary>
    /// API key for authentication with the provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL override for custom endpoints or proxies.
    /// </summary>
    /// <remarks>
    /// Use this to point to Azure OpenAI, local models, or proxy servers.
    /// </remarks>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// HTTP request timeout override for this specific provider.
    /// </summary>
    /// <remarks>
    /// If not set, uses the global <see cref="AiResilienceOptions.HttpClientTimeout"/>.
    /// </remarks>
    public TimeSpan? Timeout { get; set; }
}

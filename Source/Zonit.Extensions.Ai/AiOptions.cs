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
/// for all AI provider HTTP calls.
/// </remarks>
public sealed class AiResilienceOptions
{
    /// <summary>
    /// HTTP client timeout for requests.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(5);

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

namespace Zonit.Extensions.Ai;

/// <summary>
/// Global AI configuration options.
/// </summary>
public sealed class AiOptions
{
    /// <summary>
    /// Section name in appsettings.json: "Ai"
    /// </summary>
    public const string SectionName = "Ai";

    /// <summary>
    /// OpenAI configuration.
    /// </summary>
    public OpenAiOptions OpenAi { get; set; } = new();

    /// <summary>
    /// Anthropic configuration.
    /// </summary>
    public AnthropicOptions Anthropic { get; set; } = new();

    /// <summary>
    /// Google configuration.
    /// </summary>
    public GoogleOptions Google { get; set; } = new();

    /// <summary>
    /// X (Grok) configuration.
    /// </summary>
    public XOptions X { get; set; } = new();

    /// <summary>
    /// Resilience configuration for all providers.
    /// </summary>
    public ResilienceOptions Resilience { get; set; } = new();
}

/// <summary>
/// Resilience configuration for HTTP requests.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>
    /// HTTP client timeout.
    /// </summary>
    public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum delay between retries.
    /// </summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Use jitter for retry delays.
    /// </summary>
    public bool UseJitter { get; set; } = true;
}

/// <summary>
/// Base provider options.
/// </summary>
public abstract class ProviderOptionsBase
{
    /// <summary>
    /// API Key for the provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL override (for proxies or custom endpoints).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// HTTP request timeout override.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}

/// <summary>
/// OpenAI provider options.
/// </summary>
public sealed class OpenAiOptions : ProviderOptionsBase
{
    /// <summary>
    /// Organization ID (optional).
    /// </summary>
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Anthropic provider options.
/// </summary>
public sealed class AnthropicOptions : ProviderOptionsBase
{
}

/// <summary>
/// Google provider options.
/// </summary>
public sealed class GoogleOptions : ProviderOptionsBase
{
}

/// <summary>
/// X (Grok) provider options.
/// </summary>
public sealed class XOptions : ProviderOptionsBase
{
}

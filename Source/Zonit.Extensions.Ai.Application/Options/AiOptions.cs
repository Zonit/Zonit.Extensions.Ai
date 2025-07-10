namespace Zonit.Extensions.Ai.Application.Options;

public class AiOptions
{
    public string? OpenAiKey { get; set; }
    public string? AnthropicKey { get; set; }
    public string? XKey { get; set; }
    public string? GoogleKey { get; set; }
    
    /// <summary>
    /// Resilience configuration for all AI providers
    /// </summary>
    public ResilienceOptions Resilience { get; set; } = new();
}

public class ResilienceOptions
{
    /// <summary>
    /// HTTP client timeout for AI requests (increased for AI model processing)
    /// </summary>
    public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(45);
    
    /// <summary>
    /// Total request timeout including all retries (generous timeout for AI processing)
    /// </summary>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromMinutes(40);
    
    /// <summary>
    /// Individual attempt timeout (per single request attempt)
    /// </summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(10);
    
    /// <summary>
    /// Retry configuration with exponential backoff
    /// </summary>
    public RetryOptions Retry { get; set; } = new();
    
    /// <summary>
    /// Circuit breaker configuration to prevent cascading failures
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
}

public class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts (3 retries = 4 total attempts)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Base delay between retries (will be doubled with exponential backoff)
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(2);
    
    /// <summary>
    /// Maximum delay between retries (prevents excessive wait times)
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Whether to use jitter to prevent retry storms
    /// </summary>
    public bool UseJitter { get; set; } = true;
}

public class CircuitBreakerOptions
{
    /// <summary>
    /// Failure ratio threshold to open the circuit (50% failures)
    /// </summary>
    public double FailureRatio { get; set; } = 0.5;
    
    /// <summary>
    /// Minimum throughput required before circuit breaker activates
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;
    
    /// <summary>
    /// Duration to sample failure ratio (must be at least 2x AttemptTimeout)
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromMinutes(25);
    
    /// <summary>
    /// Duration the circuit stays open before attempting to close
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
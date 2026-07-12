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

    /// <summary>
    /// Global defaults for the agent loop (iterations, parallel tool execution,
    /// exception handling). Bound from the <c>"Ai:Agent"</c> configuration section.
    /// </summary>
    public AiAgentOptions Agent { get; set; } = new();

    /// <summary>
    /// Global outbound HTTP proxy applied to every provider's API traffic.
    /// Bound from the <c>"Ai:Proxy"</c> configuration section. Providers opt out
    /// individually via <see cref="AiProviderOptions.UseProxy"/>.
    /// </summary>
    public AiProxyOptions Proxy { get; set; } = new();
}

/// <summary>
/// Global outbound HTTP proxy for AI provider traffic. Bound from the
/// <c>"Ai:Proxy"</c> configuration section. When <see cref="Address"/> is set and
/// <see cref="Enabled"/> is <c>true</c>, every provider routes its API calls
/// through the proxy — unless that provider opts out via
/// <see cref="AiProviderOptions.UseProxy"/>.
/// </summary>
/// <remarks>
/// Typical use: reach a provider that geoblocks your region (e.g. Grok 4.5 is
/// EU-locked) by exiting through a proxy in an allowed region. Supports HTTP and
/// SOCKS proxies — set <see cref="Address"/> to e.g. <c>http://host:8080</c> or
/// <c>socks5://host:1080</c>. Leaving <see cref="Address"/> null disables the
/// proxy (default) — no proxy is applied and behavior is unchanged.
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "Proxy": {
///       "Address": "http://us-proxy.example.com:8080",
///       "Username": "user",
///       "Password": "pass"
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AiProxyOptions
{
    /// <summary>
    /// Global kill switch. When <c>false</c>, no provider uses the proxy even if
    /// <see cref="Address"/> is set. Default: <c>true</c> (proxy is active whenever
    /// an <see cref="Address"/> is configured).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Proxy URI — e.g. <c>http://host:8080</c>, <c>https://host:8443</c> or
    /// <c>socks5://host:1080</c>. <c>null</c>/empty disables the proxy entirely.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>Optional username for authenticated proxies.</summary>
    public string? Username { get; set; }

    /// <summary>Optional password for authenticated proxies.</summary>
    public string? Password { get; set; }
}

/// <summary>
/// Global defaults for agent invocations. Per-call overrides are available on
/// <see cref="Zonit.Extensions.Ai.AgentOptions"/>.
/// </summary>
/// <remarks>
/// Precedence (highest first): per-call <c>AgentOptions</c> → these globals →
/// the model's <see cref="Zonit.Extensions.Ai.IAgentLlm.DefaultMaxIterations"/>.
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "Agent": {
///       "MaxIterations": 100,
///       "MaxParallelToolCalls": 16,
///       "ToolCallTimeout": "00:02:00",
///       "OnToolException": "ReturnErrorToModel"
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AiAgentOptions
{
    /// <summary>
    /// Default maximum number of agent iterations. Large but safe ceiling —
    /// agents legitimately perform many tool calls.
    /// Default: 100.
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Maximum number of tool calls executed in parallel within a single
    /// agent iteration. Claude, GPT-5 and Gemini routinely emit multiple
    /// <c>tool_use</c> blocks in one turn; all must be executed and returned
    /// as a single batch.
    /// Default: 16. Use <see cref="int.MaxValue"/> for unlimited (explicit opt-in).
    /// </summary>
    public int MaxParallelToolCalls { get; set; } = 16;

    /// <summary>
    /// Default timeout for a single tool invocation. The runner cancels the
    /// tool and returns a timeout error to the model when exceeded.
    /// Default: 2 minutes.
    /// </summary>
    public TimeSpan ToolCallTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Behavior when a tool throws an exception.
    /// Default: <see cref="Zonit.Extensions.Ai.ToolExceptionPolicy.ReturnErrorToModel"/>.
    /// </summary>
    public Zonit.Extensions.Ai.ToolExceptionPolicy OnToolException { get; set; }
        = Zonit.Extensions.Ai.ToolExceptionPolicy.ReturnErrorToModel;

    /// <summary>
    /// Maximum depth of nested agent runs (an agent whose tool starts another
    /// agent, and so on). Exceeding it throws
    /// <see cref="Zonit.Extensions.Ai.AiNestingLimitException"/>. A runaway-recursion
    /// backstop, not a routine limit — keep it generous.
    /// Default: 16. A value &lt;= 0 disables the guard. Per-call
    /// <see cref="Zonit.Extensions.Ai.AgentOptions.MaxNestedDepth"/> overrides this.
    /// </summary>
    public int MaxNestedDepth { get; set; } = 16;

    /// <summary>
    /// Whether nested AI calls capture their prompt (<c>Input</c>) and response
    /// (<c>Output</c>) text into the usage tree (<c>AiUsageScope.Input</c> /
    /// <c>Output</c>). Tokens, cost, model and timing are always tracked; only the
    /// raw text is gated here. Turn off for PII-sensitive workloads or to cap memory.
    /// Default: true.
    /// </summary>
    public bool CaptureNestedIo { get; set; } = true;
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
///       "AttemptTimeout": "00:30:00",
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
    /// Default: 90 minutes.
    /// </summary>
    /// <remarks>
    /// This is the maximum time the request can take from start to finish,
    /// including all retries. AI requests can take many minutes, especially
    /// for complex prompts with reasoning models. Sized to accommodate the
    /// 30-minute <see cref="AttemptTimeout"/> default with up to 3 attempts.
    /// </remarks>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromMinutes(90);

    /// <summary>
    /// Timeout for a single request attempt.
    /// Default: 30 minutes.
    /// </summary>
    /// <remarks>
    /// Each individual request attempt (before retry) will timeout after this duration.
    /// Should be less than <see cref="TotalRequestTimeout"/>.
    /// Modern reasoning models (Sonnet 4.6 Medium+, Opus 4.7 high+ effort) routinely
    /// take 15-25 minutes on agentic tasks with long prompts and multiple tool calls;
    /// the previous 10-minute default cancelled them mid-thinking with
    /// <c>OperationCanceledException</c>.
    /// <para>
    /// <b>Note:</b> CircuitBreakerSamplingDuration must be at least 2x this value.
    /// </para>
    /// <para>
    /// <b>This applies only to non-streaming HTTP requests.</b> Streaming
    /// clients (agent loops, <c>ChatStreamAsync</c>) use
    /// <see cref="StreamingAttemptTimeout"/> instead — see that property
    /// for the rationale.
    /// </para>
    /// </remarks>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Per-attempt timeout for <i>streaming</i> HTTP requests (SSE-based
    /// agent loops, <c>ChatStreamAsync</c>). Default <c>null</c> means
    /// "no stricter cap than <see cref="TotalRequestTimeout"/>".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Polly's <see cref="AttemptTimeout"/> measures wall-clock time from
    /// <c>SendAsync</c> to the response body being fully read. For SSE
    /// streams that means it caps the <i>entire stream duration</i> — which
    /// happily blows past 10 minutes for Claude Sonnet with Medium+
    /// thinking on long prompts. The result is a textbook silent hang:
    /// Anthropic is busy thinking, ping events arrive every ~15 s, our SSE
    /// watchdog is happy, and Polly cancels the request at exactly
    /// <see cref="AttemptTimeout"/>.
    /// </para>
    /// <para>
    /// Stream liveness is enforced client-side via the inter-event SSE
    /// watchdog (60 s) and HTTP/2 keepalive PING frames (45 s). A
    /// per-attempt cap on top of those is structurally redundant <i>and</i>
    /// actively harmful for legitimate long-running streams. Leaving this
    /// <c>null</c> lets <see cref="TotalRequestTimeout"/> alone gate
    /// runaway streams; set it only if you want a stricter ceiling.
    /// </para>
    /// </remarks>
    public TimeSpan? StreamingAttemptTimeout { get; set; }

    /// <summary>
    /// Effective per-attempt timeout for streaming clients: explicit
    /// <see cref="StreamingAttemptTimeout"/> if set, otherwise
    /// <see cref="TotalRequestTimeout"/> (no stricter cap).
    /// </summary>
    internal TimeSpan EffectiveStreamingAttemptTimeout =>
        StreamingAttemptTimeout ?? TotalRequestTimeout;

    /// <summary>
    /// Maximum gap between two consecutive stream frames before the stream is
    /// declared dead and retried (streaming providers only; non-streaming
    /// providers ignore it). Default: 30 minutes — high-effort reasoning
    /// legitimately pauses for many minutes between frames, so a lower value
    /// trips the watchdog on healthy long thinking.
    /// </summary>
    /// <remarks>
    /// Complements the transport-layer HTTP/2 keepalive PING: this catches
    /// "server alive but application frozen" (SSE goes silent while the socket
    /// stays open), the keepalive catches a dead socket. When it fires, the
    /// retry loop re-issues the request within the <see cref="MaxRetryAttempts"/>
    /// budget, so a single stall never kills the agent run.
    /// </remarks>
    public TimeSpan InterEventTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum number of retry attempts before failing. One knob for the whole
    /// library: it bounds both the HTTP-layer retries (connection / 429 / 5xx,
    /// before a response arrives) and the client-side stream retries (an empty
    /// "200 OK" turn or a stalled/dropped stream, which the HTTP layer cannot
    /// see). Default: 6.
    /// </summary>
    /// <remarks>
    /// With the default <see cref="RetryBaseDelay"/> / <see cref="RetryMaxDelay"/>
    /// the backoff runs ≈ 5 → 10 → 20 → 40 → 60 → 60 s (~3 min total), which steps
    /// over the typical 30–90 s provider incident window instead of firing every
    /// attempt inside it. Raise it to ride out longer outages (e.g. 30 attempts at
    /// the 60 s cap ≈ 28 min). When the budget is spent on a still-empty response the
    /// call throws <see cref="Zonit.Extensions.Ai.AiEmptyResponseException"/> —
    /// never an empty result.
    /// </remarks>
    public int MaxRetryAttempts { get; set; } = 6;

    /// <summary>
    /// Base delay before the first retry; doubles each attempt up to
    /// <see cref="RetryMaxDelay"/> (exponential backoff). Default: 5 seconds.
    /// </summary>
    /// <remarks>
    /// Deliberately not sub-second: provider incident windows are typically
    /// 30–90 s, so a very short first backoff lands the next attempt back inside
    /// the bad window.
    /// </remarks>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum (cap) delay between retry attempts — the steady cadence the backoff
    /// settles into after the initial ramp. Default: 60 seconds.
    /// </summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Whether to add random jitter to retry delays to prevent thundering herd.
    /// Default: true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Duration of the sampling period for circuit breaker failure ratio calculation.
    /// Default: 75 minutes.
    /// </summary>
    /// <remarks>
    /// <b>Must be at least 2x <see cref="AttemptTimeout"/></b> (Polly requirement).
    /// Default is 75 minutes to accommodate the 30-minute attempt timeout default
    /// (Polly requires 2x AttemptTimeout; we use 2.5x for safety margin).
    /// </remarks>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromMinutes(75);

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
    /// Exponential backoff delay before the given 1-based retry
    /// <paramref name="attempt"/>, doubling from <see cref="RetryBaseDelay"/> and
    /// capped at <see cref="RetryMaxDelay"/>. Shared by the client-side stream /
    /// agent-turn retry loops so they follow the exact same schedule as the
    /// HTTP-layer Polly retries (which read the same three knobs). Deterministic
    /// (no jitter) so the schedule is predictable and testable.
    /// </summary>
    /// <param name="attempt">1-based retry number (1 = the first retry after the initial attempt).</param>
    public TimeSpan RetryDelay(int attempt)
    {
        if (attempt < 1) attempt = 1;
        var capMs = RetryMaxDelay.TotalMilliseconds;
        var ms = RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        if (double.IsNaN(ms) || ms > capMs) ms = capMs;
        return TimeSpan.FromMilliseconds(Math.Max(0, ms));
    }

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

    /// <summary>
    /// Whether this provider routes its HTTP traffic through the global proxy
    /// (<see cref="AiOptions"/>.<see cref="AiOptions.Proxy"/>) when one is
    /// configured. Default: <c>true</c> — configure a proxy once and every
    /// provider uses it. Set to <c>false</c> to exclude a single provider (e.g.
    /// keep the proxy for X/Grok but let Anthropic connect directly). Has no
    /// effect when no global proxy is configured.
    /// </summary>
    public bool UseProxy { get; set; } = true;
}

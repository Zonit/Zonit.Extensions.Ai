namespace Zonit.Extensions;

/// <summary>
/// Anthropic Claude provider configuration options.
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai:Anthropic"</c>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "Anthropic": {
///       "ApiKey": "sk-ant-...",
///       "BaseUrl": "https://api.anthropic.com"
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AnthropicOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:Anthropic";

    /// <summary>
    /// Maximum allowed gap between two consecutive SSE frames during a
    /// streaming agent turn before the stream is treated as dead and a
    /// retry is triggered.
    /// Default: 30 minutes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anthropic <i>documents</i> <c>ping</c> events every ~10–15 s during
    /// streaming, but real-world observations on Claude Sonnet 4.6 / Opus 4.7
    /// with high reasoning effort tell a much darker story:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>anthropics/claude-code#51568</c>: Extended Thinking causes
    ///     <b>451-second silent stalls</b> (7.5 min), 55 000+ tokens billed,
    ///     zero output delivered.
    ///   </description></item>
    ///   <item><description>
    ///     <c>anthropics/claude-code#54434</c>: <c>/v1/messages</c> SSE stalls
    ///     for 50–120 s without any <c>ping</c> event. Claude Code itself
    ///     defaults <c>CLAUDE_STREAM_IDLE_TIMEOUT_MS</c> to 90 s and users
    ///     routinely override it to 900 s (15 min) to absorb high-effort
    ///     thinking stalls — see <c>anthropics/claude-code#46987</c> with
    ///     40+ "Stream idle timeout - partial response received" reports.
    ///   </description></item>
    ///   <item><description>
    ///     <c>anthropics/anthropic-sdk-typescript#998</c>: the official
    ///     <c>@anthropic-ai/sdk</c> drops <c>ping</c> events entirely
    ///     (<c>src/core/streaming.ts</c> consumes them with <c>continue;</c>),
    ///     so any client built on it cannot use pings as proof-of-life.
    ///     <b>This implementation reads SSE directly and resets the watchdog
    ///     on every line</b> — including <c>ping</c> frames and even blank
    ///     separators — making us strictly more robust than Claude Code's
    ///     and Zed's content-delta-only watchdogs.
    ///   </description></item>
    /// </list>
    /// <para>
    /// 30 minutes is the right default for production agent workloads on
    /// Sonnet/Opus high-effort thinking: real-world observed responses
    /// regularly take 15–20 minutes for complex reasoning over large
    /// contexts. A value below 15 min trips the watchdog on legitimate
    /// long-running thinking. When the timeout finally fires, the
    /// <see cref="StreamMaxRetries"/> client-side retry loop re-issues
    /// the request before surfacing failure, so a single Anthropic
    /// stall never kills the entire agent run.
    /// </para>
    /// <para>
    /// Anthropic itself recommends batch processing for thinking budgets
    /// above 32k tokens (per the official Extended Thinking docs); for
    /// interactive agent flows that exceed that, expect occasional stalls
    /// regardless of timeout configuration — the retry loop is what saves
    /// the run.
    /// </para>
    /// <para>
    /// Stream liveness has two independent layers: this application-level
    /// SSE watchdog catches "server alive but app frozen", and the
    /// HTTP/2 keepalive PING (configured in
    /// <c>HttpClientBuilderExtensions.CreateAiSocketsHandler</c>: 30 s
    /// delay, 15 s timeout) catches dead sockets at the transport layer
    /// in &lt;45 s regardless of this setting.
    /// </para>
    /// </remarks>
    public TimeSpan StreamInterEventTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Number of additional attempts when a streaming turn fails with an SSE
    /// stall, mid-stream connection drop, or empty assistant turn (only
    /// <c>thinking</c> blocks, no <c>text</c> or <c>tool_use</c>).
    /// Default: 2 (= up to 3 total attempts per turn).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Polly's standard resilience handler retries only <c>HttpClient.SendAsync</c>
    /// — once SSE streaming starts, mid-stream failures are invisible to it.
    /// This setting governs the agent session's own retry loop, which re-issues
    /// the identical POST with the same message history when:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="StreamInterEventTimeout"/> elapses with no SSE event
    ///     (server-side stall — community-confirmed Anthropic incident pattern).
    ///   </description></item>
    ///   <item><description>
    ///     The TCP/HTTP-2 connection drops mid-stream
    ///     (<see cref="HttpRequestException"/> / <see cref="IOException"/>).
    ///   </description></item>
    ///   <item><description>
    ///     The model returns <c>stop_reason=end_turn</c> with only
    ///     <c>thinking</c> / <c>redacted_thinking</c> blocks (no <c>text</c>
    ///     and no <c>tool_use</c>) — observed on Sonnet 4.6 high effort as a
    ///     server-side data-loss symptom (see
    ///     <c>anthropics/anthropic-sdk-typescript#867</c>).
    ///   </description></item>
    /// </list>
    /// <para>
    /// Set to 0 to disable client-side stream retries (only Polly's
    /// <c>HttpClient.SendAsync</c>-level retry remains active).
    /// </para>
    /// </remarks>
    public int StreamMaxRetries { get; set; } = 2;

    /// <summary>
    /// Initial backoff applied between client-side stream retry attempts.
    /// Doubles on each successive retry (jittered exponential backoff).
    /// Default: 2 seconds.
    /// </summary>
    public TimeSpan StreamRetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);
}

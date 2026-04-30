namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Time-to-live for Anthropic prompt cache breakpoints
/// (<c>cache_control.ttl</c>). Selecting any non-<see cref="None"/> value
/// enables the rolling 4-breakpoint caching strategy on tools, system, and
/// the two most recent assistant messages.
/// </summary>
public enum AnthropicCacheTtl
{
    /// <summary>
    /// Caching disabled — no <c>cache_control</c> markers are added to the
    /// request. Default. Use this for one-off requests where the caching
    /// premium would not be amortised.
    /// </summary>
    None = 0,

    /// <summary>
    /// 5-minute ephemeral cache (Anthropic default TTL). Wire format:
    /// <c>cache_control: { "type": "ephemeral" }</c>. Best for short,
    /// rapid-fire agent loops where every turn lands within a few minutes.
    /// </summary>
    FiveMinutes = 1,

    /// <summary>
    /// 1-hour extended cache (beta — sends
    /// <c>anthropic-beta: extended-cache-ttl-2025-04-11</c> with each request).
    /// Wire format: <c>cache_control: { "type": "ephemeral", "ttl": "1h" }</c>.
    /// Best for long-running agent sessions or chats with idle gaps over 5 min.
    /// </summary>
    OneHour = 2,
}

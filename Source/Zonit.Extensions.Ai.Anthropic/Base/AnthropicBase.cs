namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Base class for all Anthropic Claude models.
/// </summary>
public abstract class AnthropicBase : LlmBase, ITextLlm
{
    /// <summary>
    /// Price per 1M cached write tokens.
    /// </summary>
    public abstract decimal PriceCachedWrite { get; }

    /// <summary>
    /// Price per 1M cached read tokens.
    /// </summary>
    public abstract decimal PriceCachedRead { get; }

    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => PriceCachedRead;

    /// <inheritdoc />
    public virtual decimal? PriceCachedInputWrite => PriceCachedWrite;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 1.0;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;

    /// <summary>
    /// Thinking budget in tokens for extended thinking (Claude 3.7+).
    /// </summary>
    public int? ThinkingBudget { get; set; } = null;

    /// <summary>
    /// Selects the Anthropic prompt-cache TTL applied to up to four rolling
    /// <c>cache_control</c> breakpoints (tools, system, two most recent
    /// assistant messages). Defaults to <see cref="AnthropicCacheTtl.None"/>
    /// (no caching).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AnthropicCacheTtl.FiveMinutes"/> and
    /// <see cref="AnthropicCacheTtl.OneHour"/> both pay a one-time 25% write
    /// premium on the first hit and read at ~10% of input price thereafter —
    /// net positive from the second turn of an agent / chat loop.
    /// </para>
    /// <para>
    /// <see cref="AnthropicCacheTtl.OneHour"/> is a beta feature and triggers
    /// the <c>anthropic-beta: extended-cache-ttl-2025-04-11</c> request header.
    /// </para>
    /// </remarks>
    public AnthropicCacheTtl Cache { get; set; } = AnthropicCacheTtl.None;
}

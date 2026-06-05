namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Base class for all Anthropic Claude models.
/// </summary>
public abstract class AnthropicBase : LlmBase, ITextLlm
{
    /// <summary>
    /// Provider-typed tool collection. Shadows <see cref="LlmBase.Tools"/>
    /// so the type system rejects cross-provider tool assignments at compile
    /// time — for example
    /// <c>new Sonnet46 { Tools = [new OpenAi.Tools.WebSearchTool()] }</c>
    /// will not compile.
    /// </summary>
    public new Anthropic.Tools.IAnthropicTool[]? Tools { get; init; }

    /// <inheritdoc />
    IToolBase[]? ILlm.Tools => Tools is null
        ? null
        : Array.ConvertAll(Tools, static t => (IToolBase)t);

    /// <summary>
    /// Price per 1M cache-write tokens at the 5-minute TTL (Anthropic charges
    /// 1.25× base input for this). The 1-hour TTL costs 2× base input instead and
    /// is derived from <see cref="Cache"/> in <see cref="PriceCachedInputWrite"/>,
    /// so this value is the 5-minute rate.
    /// </summary>
    public abstract decimal PriceCachedWrite { get; }

    /// <summary>
    /// Price per 1M cached read tokens.
    /// </summary>
    public abstract decimal PriceCachedRead { get; }

    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => PriceCachedRead;

    /// <summary>
    /// Effective cache-write price for the selected <see cref="Cache"/> TTL, used by
    /// the cost calculator: the 5-minute rate (<see cref="PriceCachedWrite"/>, 1.25×
    /// base) for <see cref="Anthropic.Cache.FiveMinutes"/>, and 2× base input for
    /// <see cref="Anthropic.Cache.OneHour"/> — matching Anthropic's published
    /// cache-write multipliers. Without the TTL split, 1-hour writes are under-billed.
    /// </summary>
    public virtual decimal? PriceCachedInputWrite =>
        Cache == Cache.OneHour ? PriceInput * 2m : PriceCachedWrite;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 1.0;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;

    /// <summary>
    /// Selects the Anthropic prompt-cache TTL applied to up to four rolling
    /// <c>cache_control</c> breakpoints (tools, system, two most recent
    /// assistant messages). Defaults to <see cref="Anthropic.Cache.None"/>
    /// (no caching).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cache writes carry a one-time premium on the first turn —
    /// <see cref="Anthropic.Cache.FiveMinutes"/> at 1.25× base input,
    /// <see cref="Anthropic.Cache.OneHour"/> at 2× — after which reads cost ~10% of
    /// input price. Net positive from the second turn of an agent / chat loop (after
    /// the second read for the 1-hour TTL).
    /// </para>
    /// <para>
    /// <see cref="Anthropic.Cache.OneHour"/> is a beta feature and triggers
    /// the <c>anthropic-beta: extended-cache-ttl-2025-04-11</c> request header.
    /// </para>
    /// </remarks>
    public Cache Cache { get; set; } = Cache.None;
}

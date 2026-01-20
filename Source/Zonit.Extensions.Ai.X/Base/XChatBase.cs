namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Base class for X/Grok chat models.
/// </summary>
public abstract class XChatBase : XBase, ITextLlm
{
    /// <summary>
    /// Price per 1M cached input tokens.
    /// </summary>
    public abstract decimal PriceCachedInputValue { get; }

    /// <inheritdoc />
    public decimal? PriceCachedInput => PriceCachedInputValue;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 1.0;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;

    /// <summary>
    /// Web search configuration for Grok models.
    /// </summary>
    public virtual Search WebSearch { get; init; } = new Search();
}

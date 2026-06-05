namespace Zonit.Extensions.Ai.Perplexity;

/// <summary>
/// Base class for all Perplexity models.
/// </summary>
public abstract class PerplexityBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.2;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 0.9;
}

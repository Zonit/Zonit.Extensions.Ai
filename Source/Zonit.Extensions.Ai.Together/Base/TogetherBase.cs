namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// Base class for all Together AI models.
/// </summary>
public abstract class TogetherBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.7;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 0.7;
}

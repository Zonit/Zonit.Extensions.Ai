namespace Zonit.Extensions.Ai.Zhipu;

/// <summary>
/// Base class for all Zhipu GLM models.
/// </summary>
public abstract class ZhipuBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.95;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 0.7;
}

namespace Zonit.Extensions.Ai.DeepSeek;

/// <summary>
/// Base class for all DeepSeek models.
/// </summary>
public abstract class DeepSeekBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 1.0;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;
}

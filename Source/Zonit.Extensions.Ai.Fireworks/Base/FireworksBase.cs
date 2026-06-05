namespace Zonit.Extensions.Ai.Fireworks;

/// <summary>
/// Base class for all Fireworks AI models.
/// </summary>
public abstract class FireworksBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.6;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;
}

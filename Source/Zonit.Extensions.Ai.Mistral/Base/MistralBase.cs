namespace Zonit.Extensions.Ai.Mistral;

/// <summary>
/// Base class for all Mistral models.
/// </summary>
public abstract class MistralBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.7;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;
}

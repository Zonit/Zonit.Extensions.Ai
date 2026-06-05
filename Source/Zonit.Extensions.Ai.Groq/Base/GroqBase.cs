namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Base class for all Groq models.
/// </summary>
public abstract class GroqBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 1.0;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;
}

namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Base class for all Cohere models.
/// </summary>
public abstract class CohereBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.3;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 0.75;
}

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Base class for all Google Gemini models.
/// </summary>
public abstract class GoogleBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 1.0;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 0.95;
}

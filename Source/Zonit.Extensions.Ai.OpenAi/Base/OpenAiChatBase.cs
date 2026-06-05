namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Base class for OpenAI chat models with temperature/sampling control.
/// </summary>
public abstract class OpenAiChatBase : OpenAiBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput { get; } = null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 1.0;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;
}

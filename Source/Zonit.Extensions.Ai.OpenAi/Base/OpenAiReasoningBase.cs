namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Base class for OpenAI reasoning models (O-series, GPT-5).
/// </summary>
public abstract class OpenAiReasoningBase : OpenAiBase, IReasoningLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput { get; } = null;

    /// <summary>
    /// Controls the reasoning depth for reasoning models.
    /// Higher effort results in deeper reasoning, more tokens, and potentially better accuracy.
    /// GPT-5.1 defaults to None if not specified.
    /// </summary>
    public virtual ReasoningEffort? Reason { get; init; }

    /// <summary>
    /// Controls whether and how the model's reasoning summary is returned.
    /// Only available with Responses API endpoint.
    /// </summary>
    public virtual ReasoningSummary? ReasonSummary { get; init; }

    /// <summary>
    /// Controls the verbosity of model output (GPT-5 series only).
    /// </summary>
    public virtual Verbosity? OutputVerbosity { get; init; }
}

namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that supports reasoning/thinking capabilities.
/// </summary>
public interface IReasoningLlm : ILlm
{
    /// <summary>
    /// Price per 1M cached input tokens (if supported).
    /// </summary>
    decimal? PriceCachedInput { get; }
    
    /// <summary>
    /// Reasoning effort level.
    /// </summary>
    ReasoningEffort? Reason { get; }
    
    /// <summary>
    /// Controls whether and how the model's reasoning summary is returned.
    /// </summary>
    ReasoningSummary? ReasonSummary { get; }
    
    /// <summary>
    /// Controls the verbosity of model output.
    /// </summary>
    Verbosity? OutputVerbosity { get; }
}

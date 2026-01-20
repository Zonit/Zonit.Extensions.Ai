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

    #region Legacy nested types for backward compatibility

    /// <summary>
    /// Legacy reasoning type for backward compatibility.
    /// Use <see cref="ReasoningEffort"/> instead.
    /// </summary>
    [Obsolete("Use ReasoningEffort enum instead.")]
    public enum ReasonType
    {
        /// <summary>No reasoning effort.</summary>
        None = 0,
        /// <summary>Low reasoning effort.</summary>
        Low = 1,
        /// <summary>Medium reasoning effort.</summary>
        Medium = 2,
        /// <summary>High reasoning effort.</summary>
        High = 3,
    }

    /// <summary>
    /// Legacy reasoning summary type for backward compatibility.
    /// Use <see cref="ReasoningSummary"/> instead.
    /// </summary>
    [Obsolete("Use ReasoningSummary enum instead.")]
    public enum ReasonSummaryType
    {
        /// <summary>No summary.</summary>
        None = 0,
        /// <summary>Auto summary.</summary>
        Auto = 1,
        /// <summary>Detailed summary.</summary>
        Detailed = 2,
    }

    /// <summary>
    /// Legacy verbosity type for backward compatibility.
    /// Use <see cref="Verbosity"/> instead.
    /// </summary>
    [Obsolete("Use Verbosity enum instead.")]
    public enum VerbosityType
    {
        /// <summary>Low verbosity.</summary>
        Low = 0,
        /// <summary>Medium verbosity.</summary>
        Medium = 1,
        /// <summary>High verbosity.</summary>
        High = 2,
    }

    #endregion
}

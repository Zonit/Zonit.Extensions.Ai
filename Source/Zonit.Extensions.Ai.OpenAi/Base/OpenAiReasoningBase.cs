namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Base class for OpenAI reasoning models (O-series, GPT-5).
/// </summary>
public abstract class OpenAiReasoningBase : OpenAiBase, IReasoningLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput { get; } = null;

    private ReasoningEffort? _reason;
    private ReasoningSummary? _reasonSummary;
    private Verbosity? _verbosity;

    /// <summary>
    /// Controls the reasoning depth for reasoning models using <see cref="ReasonType"/>.
    /// Higher effort results in deeper reasoning, more tokens, and potentially better accuracy.
    /// GPT-5.1 defaults to None if not specified.
    /// </summary>
    /// <example>
    /// <code>
    /// new GPT51 { Reason = OpenAiReasoningBase.ReasonType.Medium }
    /// </code>
    /// </example>
    public virtual ReasonType? Reason
    {
        get => _reason.HasValue ? (ReasonType)_reason.Value : null;
        init => _reason = value.HasValue ? (ReasoningEffort)value.Value : null;
    }

    /// <summary>
    /// Controls whether and how the model's reasoning summary is returned using <see cref="ReasonSummaryType"/>.
    /// Only available with Responses API endpoint.
    /// </summary>
    public virtual ReasonSummaryType? ReasonSummary
    {
        get => _reasonSummary.HasValue ? (ReasonSummaryType)_reasonSummary.Value : null;
        init => _reasonSummary = value.HasValue ? (ReasoningSummary)value.Value : null;
    }

    /// <summary>
    /// Controls the verbosity of model output using <see cref="VerbosityType"/> (GPT-5 series only).
    /// </summary>
    /// <example>
    /// <code>
    /// new GPT51 { Verbosity = OpenAiReasoningBase.VerbosityType.Low }
    /// </code>
    /// </example>
    public virtual VerbosityType? Verbosity
    {
        get => _verbosity.HasValue ? (VerbosityType)_verbosity.Value : null;
        init => _verbosity = value.HasValue ? (Ai.Verbosity)value.Value : null;
    }

    #region IReasoningLlm implementation (internal conversion)

    /// <summary>
    /// Internal: Gets reasoning effort for provider implementation.
    /// </summary>
    ReasoningEffort? IReasoningLlm.Reason => _reason;

    /// <summary>
    /// Internal: Gets reasoning summary for provider implementation.
    /// </summary>
    ReasoningSummary? IReasoningLlm.ReasonSummary => _reasonSummary;

    /// <summary>
    /// Internal: Gets verbosity for provider implementation.
    /// </summary>
    Verbosity? IReasoningLlm.OutputVerbosity => _verbosity;

    #endregion

    #region Nested types for model configuration

    /// <summary>
    /// Reasoning effort level for OpenAI reasoning models.
    /// </summary>
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
    /// Reasoning summary options for OpenAI models.
    /// </summary>
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
    /// Output verbosity control for GPT-5 series.
    /// </summary>
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

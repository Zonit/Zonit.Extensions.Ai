namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Base class for X/Grok reasoning models.
/// </summary>
public abstract class XReasoningBase : XChatBase, IReasoningLlm
{
    private ReasoningEffort? _reason;
    private ReasoningSummary? _reasonSummary;
    private Verbosity? _verbosity;

    /// <summary>
    /// Controls the reasoning depth for Grok reasoning models using <see cref="ReasonType"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// new Grok41FastReasoning { Reason = XReasoningBase.ReasonType.Medium }
    /// </code>
    /// </example>
    public virtual ReasonType? Reason
    {
        get => _reason.HasValue ? (ReasonType)_reason.Value : null;
        init => _reason = value.HasValue ? (ReasoningEffort)value.Value : null;
    }

    /// <summary>
    /// Controls whether and how the model's reasoning summary is returned using <see cref="ReasonSummaryType"/>.
    /// </summary>
    public virtual ReasonSummaryType? ReasonSummary
    {
        get => _reasonSummary.HasValue ? (ReasonSummaryType)_reasonSummary.Value : null;
        init => _reasonSummary = value.HasValue ? (Ai.ReasoningSummary)value.Value : null;
    }

    /// <summary>
    /// Controls the verbosity of model output using <see cref="VerbosityType"/>.
    /// </summary>
    public virtual VerbosityType? OutputVerbosity
    {
        get => _verbosity.HasValue ? (VerbosityType)_verbosity.Value : null;
        init => _verbosity = value.HasValue ? (Ai.Verbosity)value.Value : null;
    }

    #region IReasoningLlm implementation

    ReasoningEffort? IReasoningLlm.Reason => _reason;
    ReasoningSummary? IReasoningLlm.ReasonSummary => _reasonSummary;
    Verbosity? IReasoningLlm.OutputVerbosity => _verbosity;

    #endregion

    #region Nested types for model configuration

    /// <summary>
    /// Reasoning effort level for Grok reasoning models.
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
    /// Reasoning summary options for Grok models.
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
    /// Output verbosity control for Grok models.
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

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Reasoning effort levels accepted by the OpenAI o-series and the GPT-5.0–5.5
/// families. Numeric values align with the global <see cref="ReasoningEffort"/>
/// enum so the base can translate to the wire <c>reasoning.effort</c> string
/// without a per-model conversion table. Models that expose additional levels
/// (GPT-5.6+) use <see cref="OpenAiReasonEffortExtended"/> instead.
/// </summary>
public enum OpenAiReasonEffort
{
    /// <summary>No reasoning effort. Wire value <c>"none"</c>.</summary>
    None = 0,
    /// <summary>Low reasoning effort.</summary>
    Low = 1,
    /// <summary>Medium reasoning effort.</summary>
    Medium = 2,
    /// <summary>High reasoning effort.</summary>
    High = 3,
}

/// <summary>
/// Reasoning effort levels accepted by GPT-5.6 (Sol / Terra / Luna) and later —
/// the standard four plus <see cref="Xhigh"/> and <see cref="Max"/>. Numeric
/// values align with the global <see cref="ReasoningEffort"/> enum. Older OpenAI
/// reasoning models reject <see cref="Xhigh"/> / <see cref="Max"/> and therefore
/// use <see cref="OpenAiReasonEffort"/>, which does not expose them.
/// </summary>
public enum OpenAiReasonEffortExtended
{
    /// <summary>No reasoning effort. Wire value <c>"none"</c>.</summary>
    None = 0,
    /// <summary>Low reasoning effort.</summary>
    Low = 1,
    /// <summary>Medium reasoning effort.</summary>
    Medium = 2,
    /// <summary>High reasoning effort.</summary>
    High = 3,
    /// <summary>Extra effort above <see cref="High"/>. Wire value <c>"xhigh"</c>.</summary>
    Xhigh = 4,
    /// <summary>Maximum thinking budget — slowest, highest accuracy. Wire value <c>"max"</c>.</summary>
    Max = 5,
}

/// <summary>
/// Non-generic marker for OpenAI reasoning models (o-series, GPT-5+). Carries
/// the reasoning knobs shared by every tier — summary and output verbosity —
/// and lets the provider read the resolved effort without reflecting over the
/// generic <see cref="OpenAiReasoningBase{TReason}"/>. The effort <em>level</em>
/// itself is model-specific and lives on the generic subclass so each model
/// exposes only the levels its API accepts (a compile-time guard).
/// </summary>
public abstract class OpenAiReasoningBase : OpenAiBase, IReasoningLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput { get; } = null;

    private ReasoningSummary? _reasonSummary;
    private Verbosity? _verbosity;

    /// <summary>
    /// Controls whether and how the model's reasoning summary is returned using <see cref="ReasonSummaryType"/>.
    /// Only available with the Responses API endpoint.
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

    /// <summary>
    /// Resolved reasoning effort as the global <see cref="ReasoningEffort"/>, or
    /// <c>null</c> when reasoning is not set. Implemented by the generic subclass.
    /// </summary>
    protected abstract ReasoningEffort? GetReasonEffort();

    #region IReasoningLlm implementation (internal conversion)

    /// <summary>Internal: gets reasoning effort for provider implementation.</summary>
    ReasoningEffort? IReasoningLlm.Reason => GetReasonEffort();

    /// <summary>Internal: gets reasoning summary for provider implementation.</summary>
    ReasoningSummary? IReasoningLlm.ReasonSummary => _reasonSummary;

    /// <summary>Internal: gets verbosity for provider implementation.</summary>
    Verbosity? IReasoningLlm.OutputVerbosity => _verbosity;

    #endregion

    /// <summary>
    /// Maps a global <see cref="ReasoningEffort"/> to the wire-level
    /// <c>reasoning.effort</c> string accepted by OpenAI reasoning models. The
    /// C# name of the "extra" level is <see cref="ReasoningEffort.Extra"/>, but
    /// OpenAI's API expects <c>"xhigh"</c> — so the mapping is explicit rather
    /// than a naive <c>ToString()</c>. Shared by the provider and the agent
    /// session so both request paths emit identical values.
    /// </summary>
    internal static string EffortToWire(ReasoningEffort effort) => effort switch
    {
        ReasoningEffort.None => "none",
        ReasoningEffort.Low => "low",
        ReasoningEffort.Medium => "medium",
        ReasoningEffort.High => "high",
        ReasoningEffort.Extra => "xhigh",
        ReasoningEffort.Max => "max",
        _ => "high",
    };

    #region Nested types for model configuration

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

/// <summary>
/// Base class for OpenAI reasoning models (o-series, GPT-5+). Generic over
/// <typeparamref name="TReason"/> so each model exposes only the effort levels
/// its API actually accepts — passing an unsupported level is a compile-time
/// error. Use <see cref="OpenAiReasonEffort"/> for GPT-5.0–5.5 / o-series and
/// <see cref="OpenAiReasonEffortExtended"/> for GPT-5.6+.
/// </summary>
/// <typeparam name="TReason">
/// Model-specific effort enum whose numeric values align with the global
/// <see cref="ReasoningEffort"/> enum (<c>None = 0</c> … <c>Max = 5</c>).
/// </typeparam>
public abstract class OpenAiReasoningBase<TReason> : OpenAiReasoningBase
    where TReason : struct, Enum
{
    private TReason? _reason;

    /// <summary>
    /// Controls the reasoning depth for this model. Higher effort results in
    /// deeper reasoning, more tokens, and potentially better accuracy.
    /// <c>null</c> leaves the model default.
    /// </summary>
    /// <example>
    /// <code>
    /// new GPT52 { Reason = OpenAiReasonEffort.High }
    /// new Sol56 { Reason = OpenAiReasonEffortExtended.Xhigh }
    /// </code>
    /// </example>
    public virtual TReason? Reason
    {
        get => _reason;
        init => _reason = value;
    }

    /// <summary>
    /// Bridges the model-specific effort enum to the global
    /// <see cref="ReasoningEffort"/> by numeric cast — relies on
    /// <typeparamref name="TReason"/> using values aligned with
    /// <see cref="ReasoningEffort"/>.
    /// </summary>
    protected override ReasoningEffort? GetReasonEffort()
        => _reason.HasValue ? (ReasoningEffort)System.Convert.ToInt32(_reason.Value) : null;
}

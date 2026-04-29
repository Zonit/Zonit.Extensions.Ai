namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Non-generic marker for Anthropic models that support the adaptive-thinking
/// payload (<c>thinking.type = "adaptive"</c> together with a sibling
/// <c>output_config.effort</c> hint). Lets the provider branch on capability
/// without reflecting over the generic <see cref="AnthropicReasoningBase{TReason}"/>.
/// </summary>
public abstract class AnthropicAdaptiveBase : AnthropicBase, IReasoningLlm
{
    ReasoningEffort? IReasoningLlm.Reason => GetReasonEffort();
    ReasoningSummary? IReasoningLlm.ReasonSummary => null;
    Verbosity? IReasoningLlm.OutputVerbosity => null;

    /// <summary>Resolved global effort, or <c>null</c> when thinking is disabled.</summary>
    protected abstract ReasoningEffort? GetReasonEffort();
}

/// <summary>
/// Base class for Anthropic Claude models that support adaptive thinking with
/// effort control (Sonnet 4.6, Opus 4.7 and newer). Generic over
/// <typeparamref name="TReason"/> so each derived model can expose only the
/// effort levels its API actually accepts — passing an unsupported level is a
/// compile-time error.
/// </summary>
/// <typeparam name="TReason">
/// Model-specific effort enum. Numeric values must align with the global
/// <see cref="ReasoningEffort"/> enum (e.g. <c>Low = 1</c>, <c>Medium = 2</c>,
/// <c>High = 3</c>, <c>XHigh = 4</c>, <c>Max = 5</c>) so the implementation can
/// translate a model-specific value into the wire-level <c>effort</c> string
/// without per-model conversion tables.
/// </typeparam>
/// <remarks>
/// <para>
/// Adaptive thinking sends <c>"thinking": { "type": "adaptive" }</c> together
/// with a sibling <c>"output_config": { "effort": "low|medium|high|max" }</c>.
/// The model decides how many thinking tokens to consume. This replaces the
/// legacy <c>budget_tokens</c> mode (see <see cref="AnthropicBase.ThinkingBudget"/>)
/// which is still required for older models such as Sonnet 4.5 and Opus 4.5/4.6.
/// </para>
/// <para>
/// Set <see cref="Reason"/> to one of the model's nested <c>ReasonType</c>
/// values. Leaving it <c>null</c> disables thinking entirely.
/// </para>
/// </remarks>
public abstract class AnthropicReasoningBase<TReason> : AnthropicAdaptiveBase
    where TReason : struct, Enum
{
    private TReason? _reason;

    /// <summary>
    /// Adaptive thinking effort level for this model. <c>null</c> disables
    /// thinking; any other value enables adaptive thinking and is forwarded as
    /// the request-level <c>output_config.effort</c> string.
    /// </summary>
    /// <example>
    /// <code>
    /// new Sonnet46 { Reason = Sonnet46.ReasonType.High }
    /// </code>
    /// </example>
    public virtual TReason? Reason
    {
        get => _reason;
        init => _reason = value;
    }

    /// <summary>
    /// Bridges the model-specific effort enum to the global
    /// <see cref="ReasoningEffort"/> by numeric cast — relies on each
    /// <typeparamref name="TReason"/> using values aligned with
    /// <see cref="ReasoningEffort"/>.
    /// </summary>
    protected override ReasoningEffort? GetReasonEffort()
        => _reason.HasValue ? (ReasoningEffort)System.Convert.ToInt32(_reason.Value) : null;
}

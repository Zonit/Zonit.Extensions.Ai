namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Non-generic marker for Gemini models that take a <c>thinkingConfig.thinkingLevel</c>
/// (the Gemini 3.x line). Lets the provider branch on capability without reflecting
/// over the generic <see cref="GoogleThinkingBase{TReason}"/>.
/// </summary>
/// <remarks>
/// Google deprecated <c>temperature</c> / <c>top_p</c> / <c>top_k</c> on 21 July 2026
/// and recommends leaving them unset on Gemini 3.x, so <see cref="TopP"/> defaults to
/// <c>1.0</c> here — at that value the provider omits the field instead of sending the
/// <see cref="GoogleBase"/> default of <c>0.95</c>.
/// </remarks>
public abstract class GoogleThinkingBase : GoogleBase, IReasoningLlm
{
    /// <inheritdoc />
    public override double TopP { get; set; } = 1.0;

    ReasoningEffort? IReasoningLlm.Reason => GetReasonEffort();

    ReasoningSummary? IReasoningLlm.ReasonSummary => null;

    Verbosity? IReasoningLlm.OutputVerbosity => null;

    /// <summary>Resolved global effort, or <c>null</c> to take the model's default level.</summary>
    protected abstract ReasoningEffort? GetReasonEffort();
}

/// <summary>
/// Base class for Gemini 3.x models. Generic over <typeparamref name="TReason"/> so each
/// model exposes only the thinking levels its API accepts — passing an unsupported level
/// is a compile-time error.
/// </summary>
/// <typeparam name="TReason">
/// Model-specific level enum whose numeric values align with <see cref="ReasoningEffort"/>
/// (<c>Minimal = 0</c>, <c>Low = 1</c>, <c>Medium = 2</c>, <c>High = 3</c>). Gemini 3.x
/// cannot switch thinking off; the lowest level, <c>minimal</c>, occupies the
/// <see cref="ReasoningEffort.None"/> slot.
/// </typeparam>
public abstract class GoogleThinkingBase<TReason> : GoogleThinkingBase
    where TReason : struct, Enum
{
    private TReason? _reason;

    /// <summary>
    /// Thinking level, sent as <c>generationConfig.thinkingConfig.thinkingLevel</c>.
    /// <c>null</c> leaves the model's default level.
    /// </summary>
    /// <example>
    /// <code>
    /// new Gemini38Flash { Reason = Gemini38Flash.ReasonType.High }
    /// </code>
    /// </example>
    public virtual TReason? Reason
    {
        get => _reason;
        init => _reason = value;
    }

    /// <inheritdoc />
    protected override ReasoningEffort? GetReasonEffort()
        => _reason.HasValue ? (ReasoningEffort)System.Convert.ToInt32(_reason.Value) : null;
}

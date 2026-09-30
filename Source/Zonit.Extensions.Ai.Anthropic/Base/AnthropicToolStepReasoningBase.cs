namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Base class for Claude models that support <b>tool-step reasoning</b> (Anthropic
/// <c>thinking.type = "between_tools"</c>): with <see cref="ToolStepReasoning"/> on, the
/// model skips up-front thinking and reasons only after tool results arrive — without
/// tools it does not think at all. The cheapest thinking mode these models offer (they
/// cannot switch thinking off) and the natural choice for price-sensitive tool loops.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ToolStepReasoning"/> is off by default, so the model thinks normally
/// (adaptive thinking at <see cref="AnthropicReasoningBase{TReason}.Reason"/>, or the
/// server default). Turning it on keeps <c>Reason</c> as the effort of the tool-step
/// reasoning, which the API accepts only up to <c>high</c>:
/// <code>
/// new Sonnet55 { Reason = Sonnet55.ReasonType.Max }                               // adaptive, max
/// new Sonnet55 { ToolStepReasoning = true, Reason = Sonnet55.ReasonType.Medium } // tool-step, medium
/// new Sonnet55 { ToolStepReasoning = true, Reason = Sonnet55.ReasonType.Max }    // error ZAI001
/// </code>
/// </para>
/// <para>
/// The invalid combination is a compile error in object initializers (analyzer
/// <c>ZAI001</c>, driven by <see cref="CapsReasonAttribute"/>); values the compiler cannot
/// see are rejected by the provider with an <see cref="InvalidOperationException"/> before
/// anything is sent. These models also reject non-default sampling parameters (see
/// <see cref="AnthropicFixedSamplingBase{TReason}"/>).
/// </para>
/// </remarks>
public abstract class AnthropicToolStepReasoningBase<TReason> : AnthropicFixedSamplingBase<TReason>
    where TReason : struct, Enum
{
    /// <summary>
    /// Tool-step reasoning: no up-front thinking, reasoning only between tool calls (none
    /// without tools). Off by default. While on, <c>Reason</c> may be at most
    /// <c>High</c> (or unset — server default <c>high</c>).
    /// </summary>
    [CapsReason(ReasoningEffort.High)]
    public bool ToolStepReasoning { get; init; }

    /// <inheritdoc />
    protected internal override bool ReasonsAtToolStepsOnly => ToolStepReasoning;
}

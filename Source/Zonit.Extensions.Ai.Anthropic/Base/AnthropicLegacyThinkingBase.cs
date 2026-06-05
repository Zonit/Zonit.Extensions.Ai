namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Base class for Anthropic Claude models that use legacy
/// <c>budget_tokens</c> extended thinking (Sonnet 4.5, Opus 4.5, Haiku 4.5).
/// The newer adaptive thinking shape used by Sonnet 4.6 and Opus 4.6 / 4.7 / 4.8
/// lives on <see cref="AnthropicReasoningBase{TReason}"/> instead — those
/// models do <b>not</b> inherit this class and therefore do not expose a
/// <see cref="ThinkingBudget"/> knob (newer models reject <c>budget_tokens</c>
/// with a 400 or treat it as deprecated).
/// </summary>
public abstract class AnthropicLegacyThinkingBase : AnthropicBase
{
    /// <summary>
    /// Extended-thinking budget in tokens. <c>null</c> disables thinking;
    /// any positive value sends <c>thinking.type = "enabled"</c> with
    /// <c>budget_tokens = ThinkingBudget</c>. The provider also expands
    /// <c>max_tokens</c> by <c>budget + 1024</c> because Anthropic requires
    /// <c>max_tokens &gt; budget_tokens</c>.
    /// </summary>
    public int? ThinkingBudget { get; set; } = null;
}

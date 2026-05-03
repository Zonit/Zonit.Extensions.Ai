namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Base class for Anthropic Claude models that support legacy
/// <c>budget_tokens</c> extended thinking (Sonnet 4.5, Opus 4.5, Opus 4.6,
/// Haiku 4.5). The newer adaptive thinking shape used by Sonnet 4.6 / Opus 4.7
/// lives on <see cref="AnthropicReasoningBase{TReason}"/> instead — those
/// models do <b>not</b> inherit this class and therefore do not expose a
/// <see cref="ThinkingBudget"/> knob (which the API would silently ignore for
/// them).
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

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Base class for all Anthropic Claude models.
/// </summary>
public abstract class AnthropicBase : LlmBase, ITextLlm
{
    /// <summary>
    /// Price per 1M cached write tokens.
    /// </summary>
    public abstract decimal PriceCachedWrite { get; }
    
    /// <summary>
    /// Price per 1M cached read tokens.
    /// </summary>
    public abstract decimal PriceCachedRead { get; }
    
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => PriceCachedRead;
    
    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 1.0;
    
    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;

    /// <summary>
    /// Thinking budget in tokens for extended thinking (Claude 3.7+).
    /// </summary>
    public int? ThinkingBudget { get; set; } = null;
}

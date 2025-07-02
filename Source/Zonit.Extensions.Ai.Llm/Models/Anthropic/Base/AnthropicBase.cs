namespace Zonit.Extensions.Ai.Llm;

public abstract class AnthropicBase : LlmBase
{
    public abstract decimal PriceCachedWrite { get; }
    public abstract decimal PriceCachedRead { get; }

    public int? ThinkingBudget { get; set; } = null;
}
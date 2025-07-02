namespace Zonit.Extensions.Ai.Llm;

public abstract class OpenAiTextBase : OpenAiBase
{
    public virtual decimal? PriceCachedInput { get; } = null;
}

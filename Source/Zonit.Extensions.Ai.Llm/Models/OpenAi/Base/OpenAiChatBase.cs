namespace Zonit.Extensions.Ai.Llm;

public abstract class OpenAiChatBase : OpenAiBase
{
    public virtual decimal? PriceCachedInput { get; } = null;
    public virtual double Temperature { get; set; } = 1.0;
    public virtual double TopP { get; set; } = 1.0;
}
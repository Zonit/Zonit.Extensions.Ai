namespace Zonit.Extensions.Ai;

public abstract class BaseOpenAiText : BaseOpenAi
{
    public virtual decimal? PriceCachedInput { get; } = null;
}

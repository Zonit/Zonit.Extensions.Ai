namespace Zonit.Extensions.AI;

public abstract class BaseOpenAITextModel : BaseOpenAIModel
{
    public abstract decimal? PriceCachedInput { get; }
}

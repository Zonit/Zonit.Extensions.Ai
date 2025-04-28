namespace Zonit.Extensions.AI;

public abstract class BaseAnthropicModel : BaseModel
{
    public abstract decimal PriceCachedWrite { get; }
    public abstract decimal PriceCachedRead { get; }
}
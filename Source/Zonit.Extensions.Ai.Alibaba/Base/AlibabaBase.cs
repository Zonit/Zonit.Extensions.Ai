namespace Zonit.Extensions.Ai.Alibaba;

/// <summary>
/// Base class for all Alibaba/Qwen models.
/// </summary>
public abstract class AlibabaBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.85;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 0.8;
}

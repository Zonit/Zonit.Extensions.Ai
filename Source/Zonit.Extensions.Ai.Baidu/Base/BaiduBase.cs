namespace Zonit.Extensions.Ai.Baidu;

/// <summary>
/// Base class for all Baidu ERNIE models.
/// </summary>
public abstract class BaiduBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.8;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 0.8;
}

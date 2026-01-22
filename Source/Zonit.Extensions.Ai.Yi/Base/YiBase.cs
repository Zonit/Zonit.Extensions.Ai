namespace Zonit.Extensions.Ai.Yi;

/// <summary>
/// Base class for 01.AI Yi models.
/// </summary>
/// <remarks>
/// Yi is a series of high-performance language models from 01.AI, known for:
/// <list type="bullet">
///   <item>State-of-the-art bilingual (Chinese/English) capabilities</item>
///   <item>Strong long-context understanding</item>
///   <item>High performance on reasoning benchmarks</item>
///   <item>OpenAI-compatible API format</item>
/// </list>
/// </remarks>
public abstract class YiBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.3;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 0.9;
}

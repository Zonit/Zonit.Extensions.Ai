namespace Zonit.Extensions.Ai.Moonshot;

/// <summary>
/// Base class for Moonshot AI (Kimi) models.
/// </summary>
/// <remarks>
/// Moonshot AI provides the Kimi family of models, known for:
/// <list type="bullet">
///   <item>Excellent long-context understanding</item>
///   <item>Strong Chinese and English language capabilities</item>
///   <item>Context windows up to 128K tokens</item>
///   <item>OpenAI-compatible API format</item>
/// </list>
/// </remarks>
public abstract class MoonshotBase : LlmBase, ITextLlm
{
    /// <inheritdoc />
    public virtual decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public virtual double Temperature { get; set; } = 0.3;

    /// <inheritdoc />
    public virtual double TopP { get; set; } = 1.0;
}

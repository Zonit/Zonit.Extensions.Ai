namespace Zonit.Extensions.Ai;

/// <summary>
/// Reasoning effort level for reasoning models.
/// </summary>
public enum ReasoningEffort
{
    /// <summary>
    /// No reasoning effort (some models default). Fastest response.
    /// </summary>
    None,

    /// <summary>
    /// Light reasoning with quick judgment. Fast response with moderate accuracy.
    /// </summary>
    Low,

    /// <summary>
    /// Balanced depth vs speed. Safe general-purpose choice.
    /// </summary>
    Medium,

    /// <summary>
    /// Deep, multistep reasoning for complex problems.
    /// </summary>
    High,

    /// <summary>
    /// Extra-high effort. Anthropic Claude Opus 4.7 only — additional level above
    /// <see cref="High"/> that allocates substantially more thinking tokens. Not
    /// supported by OpenAI o-series, GPT-5 series, or xAI Grok models.
    /// </summary>
    XHigh,

    /// <summary>
    /// Maximum effort — model uses its full thinking capacity. Anthropic Claude
    /// Sonnet 4.6 and Opus 4.7 only (adaptive thinking). Slowest but highest
    /// accuracy. Not supported by OpenAI or xAI providers.
    /// </summary>
    Max
}

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
    /// Extra effort — additional level above <see cref="High"/> that allocates
    /// substantially more thinking tokens. Anthropic Claude Opus 4.7 / 4.8,
    /// Fable 5, Mythos 5 and Sonnet 5 only (not Sonnet 4.6 or earlier). Maps to
    /// the current API wire value <c>"xhigh"</c> (Anthropic's display name for this
    /// level is "Extra"; the wire string will likely follow). Not supported by
    /// OpenAI o-series, GPT-5 series, or xAI Grok models.
    /// </summary>
    Extra,

    /// <summary>
    /// Maximum effort — model uses its full thinking capacity. Anthropic
    /// adaptive-thinking models (Sonnet 4.6+, Opus 4.7+, Fable 5, Mythos 5).
    /// Slowest but highest accuracy. Not supported by OpenAI or xAI providers.
    /// </summary>
    Max
}

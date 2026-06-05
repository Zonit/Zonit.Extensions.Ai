namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Opus 4.7 - Most capable generally available Claude model.
/// Step-change improvement in agentic coding over Claude Opus 4.6.
/// Supports adaptive thinking with five effort levels (see <see cref="ReasonType"/>),
/// including <see cref="ReasonType.Extra"/> (an Opus-tier capability).
/// </summary>
/// <remarks>
/// 1M token context window at standard pricing (no surcharge for long context).
/// Uses a new tokenizer vs previous models — may use up to 35% more tokens for fixed text.
/// Adaptive thinking only — does not accept the legacy <c>budget_tokens</c> mode.
/// </remarks>
[Obsolete("Claude Opus 4.7 is being phased out — migrate to Opus48 (claude-opus-4-8). Still functional, but Anthropic will retire older models.")]
public class Opus47 : AnthropicReasoningBase<Opus47.ReasonType>, IAgentLlm
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Opus 4.7. Numeric
    /// values match <see cref="ReasoningEffort"/> exactly, including the
    /// <see cref="Extra"/> slot exposed by the Opus tier.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>No thinking — model responds directly.</summary>
        None = 0,
        /// <summary>Light reasoning — fastest, lowest cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning.</summary>
        High = 3,
        /// <summary>Extra effort — above <see cref="High"/> but below <see cref="Max"/>. Wire value <c>"xhigh"</c>.</summary>
        Extra = 4,
        /// <summary>Maximum thinking budget — slowest, highest accuracy.</summary>
        Max = 5,
    }

    /// <inheritdoc />
    public override string Name => "claude-opus-4-7";

    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 25.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 6.25m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.50m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_000_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 128_000;

    /// <inheritdoc />
    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output { get; } = ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.MCP;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.Reasoning;
}

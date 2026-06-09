namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Mythos 5 - Highest-capability Claude model available by invitation only
/// through Project Glasswing. New narrative-themed tier, released June 2026.
/// Supports adaptive thinking with five effort levels (see <see cref="ReasonType"/>),
/// including <see cref="ReasonType.Extra"/>.
/// </summary>
/// <remarks>
/// 1M token context window at standard pricing (no surcharge for long context).
/// Adaptive thinking only — does not accept the legacy <c>budget_tokens</c> mode.
/// Requires Project Glasswing access on the Anthropic account.
/// </remarks>
public class Mythos5 : AnthropicReasoningBase<Mythos5.ReasonType>, IAgentLlm
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Mythos 5. Numeric
    /// values match <see cref="ReasoningEffort"/> exactly, including the
    /// <see cref="Extra"/> slot.
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
    public override string Name => "claude-mythos-5";

    /// <inheritdoc />
    public override decimal PriceInput => 10.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 50.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 12.50m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 1.00m;

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

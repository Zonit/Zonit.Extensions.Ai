namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Opus 4.8 - Most capable generally available Claude model.
/// Step-change improvement in agentic coding over Claude Opus 4.7.
/// Supports adaptive thinking with five effort levels (see <see cref="ReasonType"/>),
/// including <see cref="ReasonType.Extra"/>.
/// </summary>
/// <remarks>
/// 1M token context window at standard pricing (no surcharge for long context).
/// Adaptive thinking only — does not accept the legacy <c>budget_tokens</c> mode.
/// </remarks>
public class Opus48 : AnthropicReasoningBase<Opus48.ReasonType>, IAgentLlm, IFast
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Opus 4.8. Numeric
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
    public override string Name => "claude-opus-4-8";

    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 25.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 6.25m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.50m;

    /// <summary>
    /// Inference speed. Set to <see cref="SpeedType.Fast"/> to opt this request
    /// into fast mode (up to ~2.5× output tokens/sec) at the premium fast
    /// pricing below. Requires fast-mode access on your Anthropic account
    /// (research preview, first-party API only). Defaults to
    /// <see cref="SpeedType.Standard"/>.
    /// </summary>
    public SpeedType Speed { get; init; } = SpeedType.Standard;

    /// <inheritdoc />
    public decimal FastPriceInput => 10.00m;

    /// <inheritdoc />
    public decimal FastPriceOutput => 50.00m;

    /// <summary>Fast price when <see cref="Speed"/> is <see cref="SpeedType.Fast"/>, otherwise the standard price.</summary>
    public override decimal GetInputPrice(long tokenCount)
        => Speed == SpeedType.Fast ? FastPriceInput : PriceInput;

    /// <summary>Fast price when <see cref="Speed"/> is <see cref="SpeedType.Fast"/>, otherwise the standard price.</summary>
    public override decimal GetOutputPrice(long tokenCount)
        => Speed == SpeedType.Fast ? FastPriceOutput : PriceOutput;

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

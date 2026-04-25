namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Sonnet 4.6 - Best combination of speed and intelligence.
/// Near-Opus-level coding with improved instruction-following and tool reliability.
/// Supports extended thinking and adaptive thinking.
/// </summary>
/// <remarks>
/// 1M token context window at standard pricing (no surcharge for long context).
/// </remarks>
public class Sonnet46 : AnthropicBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "claude-sonnet-4-6";

    /// <inheritdoc />
    public override decimal PriceInput => 3.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 15.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 3.75m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.30m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_000_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 64_000;

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

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Opus 4.7 - Most capable generally available Claude model.
/// Step-change improvement in agentic coding over Claude Opus 4.6.
/// Supports adaptive thinking (not extended thinking).
/// </summary>
/// <remarks>
/// 1M token context window at standard pricing (no surcharge for long context).
/// Uses a new tokenizer vs previous models — may use up to 35% more tokens for fixed text.
/// </remarks>
public class Opus47 : AnthropicBase
{
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

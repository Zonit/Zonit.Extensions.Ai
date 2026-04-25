namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Sonnet 4.5 - Our smart model for complex agents and coding.
/// Best balance of intelligence, speed, and cost for most use cases.
/// </summary>
public class Sonnet45 : AnthropicBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "claude-sonnet-4-5-20250929";

    /// <inheritdoc />
    public override decimal PriceInput => 3.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 15.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 3.75m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.30m;

    /// <inheritdoc />
    public override int MaxInputTokens => 200_000;

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

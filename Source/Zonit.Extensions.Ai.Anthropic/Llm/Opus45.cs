namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Opus 4.5 - Premium model combining maximum intelligence with practical performance.
/// Most capable Anthropic model.
/// </summary>
public class Opus45 : AnthropicBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "claude-opus-4-5-20251101";

    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 25.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 6.25m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.50m;

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

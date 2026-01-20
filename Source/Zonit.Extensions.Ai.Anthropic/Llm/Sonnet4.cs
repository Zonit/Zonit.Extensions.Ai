namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude 4 Sonnet - Balanced performance and cost.
/// </summary>
public class Sonnet4 : AnthropicBase
{
    /// <inheritdoc />
    public override string Name => "claude-sonnet-4-20250514";

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
}

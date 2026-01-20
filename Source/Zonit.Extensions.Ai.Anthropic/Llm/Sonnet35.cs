namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude 3.5 Sonnet - Previous generation balanced model.
/// Cost-effective option with good performance.
/// </summary>
public class Sonnet35 : AnthropicBase
{
    /// <inheritdoc />
    public override string Name => "claude-3-5-sonnet-20241022";

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
    public override int MaxOutputTokens => 8_192;

    /// <inheritdoc />
    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output { get; } = ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.MCP;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;
}

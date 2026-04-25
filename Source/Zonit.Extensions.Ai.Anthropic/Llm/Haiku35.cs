namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude 3.5 Haiku - Fast and cost-effective legacy model.
/// Good for simple tasks and high-volume applications.
/// </summary>
public class Haiku35 : AnthropicBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "claude-3-5-haiku-20241022";

    /// <inheritdoc />
    public override decimal PriceInput => 0.80m;

    /// <inheritdoc />
    public override decimal PriceOutput => 4.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 1.00m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.08m;

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

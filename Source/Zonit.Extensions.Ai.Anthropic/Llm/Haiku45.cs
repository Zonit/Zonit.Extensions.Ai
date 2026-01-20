namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude 4.5 Haiku - Fast and cost-effective.
/// </summary>
public class Haiku45 : AnthropicBase
{
    /// <inheritdoc />
    public override string Name => "claude-haiku-4-5-20251001";

    /// <inheritdoc />
    public override decimal PriceInput => 1.00m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 5.00m;
    
    /// <inheritdoc />
    public override decimal PriceCachedWrite => 1.25m;
    
    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.10m;
    
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

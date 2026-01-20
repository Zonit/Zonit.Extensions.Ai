namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude 4 Opus - Most capable Claude model.
/// </summary>
public class Opus4 : AnthropicBase
{
    /// <inheritdoc />
    public override string Name => "claude-opus-4-20250514";

    /// <inheritdoc />
    public override decimal PriceInput => 15.00m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 75.00m;
    
    /// <inheritdoc />
    public override decimal PriceCachedWrite => 18.75m;
    
    /// <inheritdoc />
    public override decimal PriceCachedRead => 1.50m;
    
    /// <inheritdoc />
    public override int MaxInputTokens => 200_000;
    
    /// <inheritdoc />
    public override int MaxOutputTokens => 32_000;

    /// <inheritdoc />
    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;
    
    /// <inheritdoc />
    public override ChannelType Output { get; } = ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.MCP;
    
    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;
}

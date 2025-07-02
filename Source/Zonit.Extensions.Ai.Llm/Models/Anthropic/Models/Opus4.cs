namespace Zonit.Extensions.Ai.Llm.Anthropic;

public class Opus4 : AnthropicBase
{
    public override string Name => "claude-opus-4-20250514";

    public override decimal PriceInput => 0.015m; // $15 per MTok
    public override decimal PriceOutput => 0.075m; // $75 per MTok
    public override decimal PriceCachedWrite => 0.01875m; // $18.75 per MTok for 5m cache writes
    public override decimal PriceCachedRead => 0.0015m; // $1.50 per MTok for cache hits & refreshes

    public override int MaxInputTokens => 200000; // 200k context window
    public override int MaxOutputTokens => 32000; // ~24k words, 32k tokens max output

    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;
    public override ChannelType Output { get; } = ChannelType.Text;


    public override ToolsType Tools => ToolsType.WebSearch | ToolsType.MCP;
    public override EndpointsType Endpoints => EndpointsType.Chat | EndpointsType.Response; 
}
namespace Zonit.Extensions.Ai.Llm.Anthropic;

public class Sonnet4 : AnthropicBase
{
    public override string Name => "claude-sonnet-4-20250514";

    public override decimal PriceInput => 0.003m; // $3 per MTok
    public override decimal PriceOutput => 0.015m; // $15 per MTok
    public override decimal PriceCachedWrite => 0.00375m; // $3.75 per MTok for 5m cache writes
    public override decimal PriceCachedRead => 0.0003m; // $0.30 per MTok for cache hits & refreshes

    public override int MaxInputTokens => 200000; // 200k context window
    public override int MaxOutputTokens => 64000; // ~48k words, 64k tokens max output

    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;
    public override ChannelType Output { get; } = ChannelType.Text;

    public override ToolsType Tools => ToolsType.WebSearch | ToolsType.MCP;
    public override EndpointsType Endpoints => EndpointsType.Chat | EndpointsType.Response;
}
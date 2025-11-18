namespace Zonit.Extensions.Ai.Llm.Anthropic;

public class Haiku45 : AnthropicBase
{
    public override string Name => "claude-haiku-4-5-20251001";

    public override decimal PriceInput => 0.001m; // $1 per MTok
    public override decimal PriceOutput => 0.005m; // $5 per MTok
    public override decimal PriceCachedWrite => 0.00125m; // $1.25 per MTok for 5m cache writes
    public override decimal PriceCachedRead => 0.0001m; // $0.10 per MTok for cache hits & refreshes

    public override int MaxInputTokens => 200000; // 200k context window
    public override int MaxOutputTokens => 64000; // 64k tokens max output

    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;
    public override ChannelType Output { get; } = ChannelType.Text;

    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.MCP;
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;
}

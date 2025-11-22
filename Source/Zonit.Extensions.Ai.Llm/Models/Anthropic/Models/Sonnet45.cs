namespace Zonit.Extensions.Ai.Llm.Anthropic;

public class Sonnet45 : AnthropicBase
{
    public override string Name => "claude-sonnet-4-5-20250929";

    public override decimal PriceInput => 3.00m; // $3 per MTok
    public override decimal PriceOutput => 15.00m; // $15 per MTok
    public override decimal PriceCachedWrite => 3.75m; // $3.75 per MTok for 5m cache writes
    public override decimal PriceCachedRead => 0.30m; // $0.30 per MTok for cache hits & refreshes

    public override int MaxInputTokens => 200000; // 200k context window / 1M tokens (beta)
    public override int MaxOutputTokens => 64000; // 64k tokens max output

    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;
    public override ChannelType Output { get; } = ChannelType.Text;

    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.MCP;
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;
}

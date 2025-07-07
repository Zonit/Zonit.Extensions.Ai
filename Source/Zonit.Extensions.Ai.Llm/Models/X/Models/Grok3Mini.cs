namespace Zonit.Extensions.Ai.Llm.X;

public class Grok3Mini : XReasoningBase
{
    public override string Name => "grok-3-mini";

    public override decimal PriceInput => 0.30m;

    public override decimal PriceCachedInput => 0.075m;

    public override decimal PriceOutput => 0.50m;

    public override int MaxInputTokens => 131_072;

    public override int MaxOutputTokens => 8_192;

    public override ChannelType Input => ChannelType.Text;

    public override ChannelType Output => ChannelType.Text;

    public override ToolsType Tools => ToolsType.WebSearch;

    public override EndpointsType Endpoints => EndpointsType.Chat;
}
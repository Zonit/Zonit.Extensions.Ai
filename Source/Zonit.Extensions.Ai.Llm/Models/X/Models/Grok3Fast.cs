namespace Zonit.Extensions.Ai.Llm.X;

public class Grok3Fast : XChatBase
{
    public override string Name => "grok-3-fast";

    public override decimal PriceInput => 5.00m;

    public override decimal PriceCachedInput => 1.25m;

    public override decimal PriceOutput => 25.00m;

    public override int MaxInputTokens => 131_072;

    public override int MaxOutputTokens => 8_192;

    public override ChannelType Input => ChannelType.Text;

    public override ChannelType Output => ChannelType.Text;

    public override ToolsType Tools => ToolsType.WebSearch;

    public override EndpointsType Endpoints => EndpointsType.Chat;
    public override FeaturesType Features =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;
}
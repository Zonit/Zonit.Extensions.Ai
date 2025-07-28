namespace Zonit.Extensions.Ai.Llm.X;

public class Grok3 : XChatBase
{
    public override string Name => "grok-3";

    public override decimal PriceInput => 3.00m;

    public override decimal PriceCachedInput => 0.75m;

    public override decimal PriceOutput => 15.00m;

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

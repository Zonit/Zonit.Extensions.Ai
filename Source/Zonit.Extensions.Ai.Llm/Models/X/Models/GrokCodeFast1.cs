namespace Zonit.Extensions.Ai.Llm.X;

public class GrokCodeFast1 : XChatBase
{
    public override string Name => "grok-code-fast-1-0825";

    public override decimal PriceInput => 0.20m;

    public override decimal PriceCachedInput => 0.05m;

    public override decimal PriceOutput => 1.50m;

    public override int MaxInputTokens => 256_000;

    public override int MaxOutputTokens => 480_000;

    public override ChannelType Input => ChannelType.Text;

    public override ChannelType Output => ChannelType.Text;

    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
    
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;
}

namespace Zonit.Extensions.Ai.Llm.X;

public class Grok4FastReasoning : XReasoningBase
{
    public override string Name => "grok-4-fast-reasoning";

    public override decimal PriceInput => 0.20m;

    public override decimal PriceCachedInput => 0.05m;

    public override decimal PriceOutput => 0.50m;

    public override int MaxInputTokens => 2_000_000;

    public override int MaxOutputTokens => 4_000_000;

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    public override ChannelType Output => ChannelType.Text;

    public override ToolsType SupportedTools => ToolsType.WebSearch;

    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
    
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 128_000 ? PriceInput * 2 : PriceInput;
    }

    public override decimal GetOutputPrice(long tokenCount)
    {
        return tokenCount > 128_000 ? PriceCachedInput * 2 : PriceCachedInput;
    }
}

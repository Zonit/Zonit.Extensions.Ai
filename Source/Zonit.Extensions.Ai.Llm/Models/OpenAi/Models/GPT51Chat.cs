namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class GPT51Chat : OpenAiReasoningBase
{
    public override string Name => "gpt-5.1-chat";

    // Pricing per 1M tokens from OpenAI documentation
    public override decimal PriceInput => 1.25m;
    public override decimal PriceOutput => 10.00m;
    public override decimal? PriceCachedInput => 0.125m;
    public override decimal? BatchPriceInput => null; // Batch pricing not specified
    public override decimal? BatchPriceOutput => null;

    public override int MaxInputTokens => 128_000;
    public override int MaxOutputTokens => 16_384;

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    public override ChannelType Output => ChannelType.Text;

    public override ToolsType SupportedTools => ToolsType.None;

    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response;
}

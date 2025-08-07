namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class GPT5Nano : OpenAiReasoningBase
{
    public override string Name => "gpt-5-nano-2025-08-07";

    // Pricing per 1M tokens from OpenAI documentation
    public override decimal PriceInput => 0.05m;
    public override decimal PriceOutput => 0.40m;
    public override decimal? PriceCachedInput => 0.005m;
    public override decimal? BatchPriceInput => 0.025m;
    public override decimal? BatchPriceOutput => 0.20m;

    public override int MaxInputTokens => 400_000;
    public override int MaxOutputTokens => 128_000;

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    public override ChannelType Output => ChannelType.Text;

    public override ToolsType Tools =>
        ToolsType.ImageGeneration |
        ToolsType.FileSearch |
        ToolsType.CodeInterpreter |
        ToolsType.MCP;

    public override FeaturesType Features =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.FineTuning;

    public override EndpointsType Endpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Assistant |
        EndpointsType.Batch;
}
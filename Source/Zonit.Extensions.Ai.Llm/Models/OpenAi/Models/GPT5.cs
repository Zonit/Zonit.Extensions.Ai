namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class GPT5 : OpenAiReasoningBase
{
    public override string Name => "gpt-5-2025-08-07";

    // Pricing per 1M tokens from OpenAI documentation
    public override decimal PriceInput => 1.25m;
    public override decimal PriceOutput => 10.00m;
    public override decimal? PriceCachedInput => 0.125m;
    public override decimal? BatchPriceInput => 0.625m;
    public override decimal? BatchPriceOutput => 5.00m;

    public override int MaxInputTokens => 400_000;
    public override int MaxOutputTokens => 128_000;

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    public override ChannelType Output => ChannelType.Text;

    public override ToolsType SupportedTools =>
        ToolsType.WebSearch |
        ToolsType.FileSearch |
        ToolsType.ImageGeneration |
        ToolsType.CodeInterpreter |
        ToolsType.MCP;

    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.FineTuning |
        FeaturesType.Distillation |
        FeaturesType.PredictedOutputs;

    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Assistant |
        EndpointsType.Batch;
}
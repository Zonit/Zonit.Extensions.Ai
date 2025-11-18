namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class GPT51 : OpenAiReasoningBase
{
    public override string Name => "gpt-5.1-2025-11-13";

    // Pricing per 1M tokens from OpenAI documentation
    public override decimal PriceInput => 1.25m;
    public override decimal PriceOutput => 10.00m;
    public override decimal? PriceCachedInput => 0.125m;
    public override decimal? BatchPriceInput => null; // Batch pricing not specified
    public override decimal? BatchPriceOutput => null;

    public override int MaxInputTokens => 400_000;
    public override int MaxOutputTokens => 128_000;

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    public override ChannelType Output => ChannelType.Text | ChannelType.Image;

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
        FeaturesType.Distillation;

    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response;
}

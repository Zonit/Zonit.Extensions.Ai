namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class O4Mini : OpenAiReasoningBase
{
    public override string Name => "o4-mini-2025-04-16";

    // Pricing per 1M tokens from the OpenAI documentation
    public override decimal PriceInput => 1.10m;
    public override decimal PriceOutput => 4.40m;
    public override decimal? PriceCachedInput => 0.275m;
    public override decimal? BatchPriceInput => 0.55m;
    public override decimal? BatchPriceOutput => 2.20m;

    public override int MaxInputTokens => 200_000;
    public override int MaxOutputTokens => 100_000;

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    public override ChannelType Output => ChannelType.Text;

    public override ToolsType Tools =>
        ToolsType.WebSearch |
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
        EndpointsType.Batch | 
        EndpointsType.FineTuning;
}
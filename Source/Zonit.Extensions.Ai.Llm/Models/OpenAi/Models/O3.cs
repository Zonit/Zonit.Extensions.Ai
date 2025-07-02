namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class O3 : OpenAiReasoningBase
{
    public override string Name => "o3-2025-04-16";

    public override decimal PriceInput => 2.00m;
    public override decimal PriceOutput => 8.00m;
    public override decimal? PriceCachedInput => 0.50m;
    public override decimal? BatchPriceInput => 1.00m;
    public override decimal? BatchPriceOutput => 4.00m;

    public override int MaxInputTokens => 200_000;
    public override int MaxOutputTokens => 100_000;

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    public override ChannelType Output => ChannelType.Text;

    public override ToolsType Tools =>
        ToolsType.WebSearch |
        ToolsType.FileSearch |
        ToolsType.ImageGeneration |
        ToolsType.CodeInterpreter |
        ToolsType.MCP;

    public override FeaturesType Features =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    public override EndpointsType Endpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Batch;
}

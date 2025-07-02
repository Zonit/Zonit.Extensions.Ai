namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class GPT41 : OpenAiChatBase
{
    public override string Name => "gpt-4.1-2025-04-14";

    public override decimal PriceInput => 2.00m;
    public override decimal PriceOutput => 8.00m;
    public override decimal? PriceCachedInput => 0.50m;
    public override decimal? BatchPriceInput => 1.00m;
    public override decimal? BatchPriceOutput => 4.00m;

    public override int MaxInputTokens => 1_047_576;
    public override int MaxOutputTokens => 32_768;

    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;
    public override ChannelType Output { get; } = ChannelType.Text;

    public override ToolsType Tools => 
        ToolsType.WebSearch | 
        ToolsType.FileSearch | 
        ToolsType.ImageGeneration | 
        ToolsType.CodeInterpreter | 
        ToolsType.MCP;

    public override FeaturesType Features => 
        FeaturesType.Streaming | 
        FeaturesType.FunctionCalling | 
        FeaturesType.StructuredOutputs | 
        FeaturesType.FineTuning | 
        FeaturesType.Distillation | 
        FeaturesType.PredictedOutputs;

    public override EndpointsType Endpoints => 
        EndpointsType.Chat | 
        EndpointsType.Response | 
        EndpointsType.Assistant | 
        EndpointsType.Batch | 
        EndpointsType.FineTuning;
}
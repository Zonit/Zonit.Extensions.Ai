namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class GPT41Nano : OpenAiChatBase
{
    public override string Name => "gpt-4.1-nano-2025-04-14";

    public override decimal PriceInput => 0.10m;
    public override decimal PriceOutput => 0.40m;
    public override decimal? PriceCachedInput => 0.025m;
    public override decimal? BatchPriceInput => 0.05m;
    public override decimal? BatchPriceOutput => 0.20m;

    public override int MaxInputTokens => 1_047_576;
    public override int MaxOutputTokens => 32_768;

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    public override ChannelType Output => ChannelType.Text;

    public override ToolsType SupportedTools =>
        ToolsType.FileSearch |
        ToolsType.ImageGeneration |
        ToolsType.CodeInterpreter |
        ToolsType.MCP;

    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.FineTuning;

    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Assistant |
        EndpointsType.Batch |
        EndpointsType.FineTuning;
}
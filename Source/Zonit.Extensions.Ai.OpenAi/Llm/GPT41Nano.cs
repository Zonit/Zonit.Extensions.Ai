namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-4.1 Nano - Smallest and most cost-effective GPT-4.1 variant.
/// </summary>
public class GPT41Nano : OpenAiChatBase
{
    /// <inheritdoc />
    public override string Name => "gpt-4.1-nano-2025-04-14";

    /// <inheritdoc />
    public override decimal PriceInput => 0.10m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 0.40m;
    
    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.025m;
    
    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.05m;
    
    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 0.20m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_047_576;
    
    /// <inheritdoc />
    public override int MaxOutputTokens => 32_768;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    
    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools =>
        ToolsType.FileSearch |
        ToolsType.ImageGeneration |
        ToolsType.CodeInterpreter |
        ToolsType.MCP;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.FineTuning;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Assistant |
        EndpointsType.Batch |
        EndpointsType.FineTuning;
}

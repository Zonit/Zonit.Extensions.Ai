namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// O3 - OpenAI's reasoning model optimized for complex problems.
/// </summary>
public class O3 : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "o3-2025-04-16";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 8.00m;
    
    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.50m;
    
    /// <inheritdoc />
    public override decimal? BatchPriceInput => 1.00m;
    
    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 4.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 200_000;
    
    /// <inheritdoc />
    public override int MaxOutputTokens => 100_000;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    
    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools =>
        ToolsType.WebSearch |
        ToolsType.FileSearch |
        ToolsType.ImageGeneration |
        ToolsType.CodeInterpreter |
        ToolsType.MCP;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Batch;
}

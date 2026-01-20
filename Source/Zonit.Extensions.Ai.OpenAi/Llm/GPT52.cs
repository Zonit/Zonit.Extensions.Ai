namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.2 - The best model for coding and agentic tasks across industries.
/// Latest and most capable OpenAI model.
/// </summary>
public class GPT52 : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "gpt-5.2";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;
    
    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.50m;
    
    /// <inheritdoc />
    public override decimal? BatchPriceInput => 1.00m;
    
    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 5.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 400_000;
    
    /// <inheritdoc />
    public override int MaxOutputTokens => 128_000;

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
        FeaturesType.StructuredOutputs |
        FeaturesType.FineTuning |
        FeaturesType.Distillation |
        FeaturesType.PredictedOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Assistant |
        EndpointsType.Batch;
}

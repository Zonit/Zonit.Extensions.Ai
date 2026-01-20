namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5-mini - A faster, cost-efficient version of GPT-5 for well-defined tasks.
/// Good balance between performance and cost.
/// </summary>
public class GPT5Mini : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "gpt-5-mini";

    /// <inheritdoc />
    public override decimal PriceInput => 0.50m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 2.00m;
    
    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.125m;
    
    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.25m;
    
    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 1.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 256_000;
    
    /// <inheritdoc />
    public override int MaxOutputTokens => 64_000;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    
    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools =>
        ToolsType.WebSearch |
        ToolsType.FileSearch |
        ToolsType.CodeInterpreter;

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
        EndpointsType.Batch;
}

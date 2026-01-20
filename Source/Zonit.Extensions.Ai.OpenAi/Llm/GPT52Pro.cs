namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.2-pro - Version of GPT-5.2 that produces smarter and more precise responses.
/// Uses more compute for higher quality outputs.
/// </summary>
public class GPT52Pro : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "gpt-5.2-pro";

    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 25.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 1.25m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 2.50m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 12.50m;

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

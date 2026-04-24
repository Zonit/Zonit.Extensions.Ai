namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.4 Nano - Fastest, most cost-efficient GPT-5.4 variant.
/// Optimized for simple, high-volume tasks with 400K context window.
/// </summary>
public class GPT54Nano : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "gpt-5.4-nano";

    /// <inheritdoc />
    public override decimal PriceInput => 0.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.25m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.02m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.10m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 0.625m;

    /// <inheritdoc />
    public override int MaxInputTokens => 400_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 32_000;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools =>
        ToolsType.WebSearch |
        ToolsType.FileSearch;

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

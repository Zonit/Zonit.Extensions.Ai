namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5-nano - Fastest, most cost-efficient version of GPT-5.
/// Best for simple, high-volume tasks.
/// </summary>
public class GPT5Nano : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5-nano";

    /// <inheritdoc />
    public override decimal PriceInput => 0.05m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.40m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.005m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.05m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 0.20m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

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

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// O3 Mini - A small model alternative to O3.
/// Cost-efficient reasoning model.
/// </summary>
public class O3Mini : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "o3-mini";

    /// <inheritdoc />
    public override decimal PriceInput => 1.10m;

    /// <inheritdoc />
    public override decimal PriceOutput => 4.40m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.55m;

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
        ToolsType.CodeInterpreter;

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

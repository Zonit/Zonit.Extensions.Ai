namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// O1 - Previous full O-series reasoning model.
/// </summary>
public class O1 : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "o1";

    /// <inheritdoc />
    public override decimal PriceInput => 15.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 60.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 7.50m;

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

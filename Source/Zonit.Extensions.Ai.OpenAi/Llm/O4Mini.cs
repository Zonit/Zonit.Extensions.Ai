namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// O4 Mini - Faster and more cost-effective reasoning model.
/// </summary>
public class O4Mini : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "o4-mini-2025-04-16";

    /// <inheritdoc />
    public override decimal PriceInput => 1.10m;

    /// <inheritdoc />
    public override decimal PriceOutput => 4.40m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.275m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.55m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 2.20m;

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
        EndpointsType.Batch |
        EndpointsType.FineTuning;
}

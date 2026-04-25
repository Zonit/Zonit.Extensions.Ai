namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// o3-pro - Version of o3 with more compute for better responses.
/// Premium reasoning model with highest accuracy.
/// </summary>
public class O3Pro : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "o3-pro";

    /// <inheritdoc />
    public override decimal PriceInput => 20.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 80.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 5.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 10.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 40.00m;

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

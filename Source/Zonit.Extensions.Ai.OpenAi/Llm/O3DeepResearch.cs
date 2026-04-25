namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// O3 Deep Research - OpenAI's most advanced research model.
/// </summary>
public class O3DeepResearch : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "o3-deep-research";

    /// <inheritdoc />
    public override decimal PriceInput => 10.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 40.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 2.50m;

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
        EndpointsType.Response;
}

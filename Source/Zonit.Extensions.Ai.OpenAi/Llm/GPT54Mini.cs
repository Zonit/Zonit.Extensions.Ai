namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.4 Mini - Faster, more efficient GPT-5.4 variant for high-volume workloads.
/// Brings GPT-5.4-class capabilities at lower cost and latency.
/// </summary>
public class GPT54Mini : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.4-mini";

    /// <inheritdoc />
    public override decimal PriceInput => 0.75m;

    /// <inheritdoc />
    public override decimal PriceOutput => 4.50m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.075m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.375m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 2.25m;

    /// <inheritdoc />
    public override int MaxInputTokens => 400_000;

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
        EndpointsType.Assistant |
        EndpointsType.Batch;
}

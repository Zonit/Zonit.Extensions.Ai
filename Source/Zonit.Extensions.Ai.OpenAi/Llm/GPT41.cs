namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-4.1 - Latest GPT-4 model with excellent instruction following.
/// </summary>
public class GPT41 : OpenAiChatBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-4.1-2025-04-14";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 8.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.50m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 1.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 4.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_047_576;

    /// <inheritdoc />
    public override int MaxOutputTokens => 32_768;

    /// <inheritdoc />
    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output { get; } = ChannelType.Text;

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
        EndpointsType.Batch |
        EndpointsType.FineTuning;
}

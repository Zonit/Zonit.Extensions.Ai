namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.4 - OpenAI's flagship model for complex reasoning and coding.
/// Incorporates frontier coding capabilities of GPT-5.3-Codex.
/// 1.1M token context window; standard pricing applies up to 272K tokens,
/// input cost doubles beyond 272K.
/// </summary>
public class GPT54 : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.4";

    /// <inheritdoc />
    public override decimal PriceInput => 2.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 15.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.25m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 1.25m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 7.50m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_100_000;

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

    /// <summary>
    /// Extended context pricing: input cost doubles above 272K tokens.
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 272_000 ? PriceInput * 2 : PriceInput;
    }
}

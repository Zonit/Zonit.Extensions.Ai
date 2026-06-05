namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.5 — OpenAI's current flagship model for complex reasoning, coding and
/// professional work. Supersedes GPT-5.4 on the <c>gpt-5.5</c> alias.
/// 1.05M token context window; standard pricing applies up to 272K tokens,
/// input doubles and output is 1.5× beyond 272K (per OpenAI pricing notes).
/// </summary>
public class GPT55 : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.5";

    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 30.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.50m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 2.50m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 15.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_050_000;

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
        FeaturesType.PredictedOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Batch;

    /// <summary>
    /// Extended-context pricing: inputs beyond 272K tokens are billed at 2×
    /// the base rate for the remainder of the session (standard, batch, flex).
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 272_000 ? PriceInput * 2 : PriceInput;
    }
}

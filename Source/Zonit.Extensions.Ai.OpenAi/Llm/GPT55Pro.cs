namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.5 Pro — highest-tier GPT-5.5 variant for the most demanding
/// reasoning and coding workloads. Premium compute budget; prefer GPT-5.5
/// for most tasks and reach for Pro only when the base model runs out of
/// headroom.
/// </summary>
/// <remarks>
/// $30 / $180 per 1M tokens (standard). Batch pricing available at half the
/// standard rate. No cached input discount. Responses API and Batch API only.
/// </remarks>
public class GPT55Pro : OpenAiReasoningBase<OpenAiReasonEffort>, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.5-pro";

    /// <inheritdoc />
    public override decimal PriceInput => 30.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 180.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 15.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 90.00m;

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
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Response |
        EndpointsType.Batch;

    /// <summary>
    /// Extended-context pricing: inputs beyond 272K tokens are billed at 2×
    /// the base rate for the remainder of the session.
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 272_000 ? PriceInput * 2 : PriceInput;
    }
}

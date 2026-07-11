namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.6 Sol — the frontier tier of the GPT-5.6 family, for the hardest
/// reasoning, coding and security-research workloads. Sol, Terra and Luna are
/// durable capability tiers (see <see cref="Terra56"/>, <see cref="Luna56"/>)
/// that advance on their own cadence, replacing the earlier unsuffixed / mini /
/// nano naming. Sol occupies the slot previously held by the unsuffixed model
/// (e.g. <see cref="GPT55"/>).
/// </summary>
/// <remarks>
/// 1.05M token context window; standard pricing applies up to 272K tokens,
/// input doubles beyond 272K (per OpenAI pricing notes). Model id
/// <c>gpt-5.6-sol</c> (also aliased <c>gpt-5.6</c>). Supports the full
/// reasoning range none / low / medium / high / <see cref="OpenAiReasonEffortExtended.Xhigh"/>
/// / <see cref="OpenAiReasonEffortExtended.Max"/>.
/// </remarks>
public class Sol56 : OpenAiReasoningBase<OpenAiReasonEffortExtended>, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.6-sol";

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

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.6 Terra — the balanced tier of the GPT-5.6 family, tuned for
/// high-volume business tasks (support, internal tools, document analysis)
/// where intelligence and cost must be balanced. Occupies the slot previously
/// held by the <c>-mini</c> models. See <see cref="Sol56"/> (frontier) and
/// <see cref="Luna56"/> (fast/low-cost).
/// </summary>
/// <remarks>
/// 1.05M token context window; standard pricing applies up to 272K tokens,
/// input doubles beyond 272K. Wire alias <c>gpt-5.6-terra</c>.
/// </remarks>
public class Terra56 : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.6-terra";

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

    /// <summary>
    /// Extended-context pricing: inputs beyond 272K tokens are billed at 2×
    /// the base rate for the remainder of the session.
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 272_000 ? PriceInput * 2 : PriceInput;
    }
}

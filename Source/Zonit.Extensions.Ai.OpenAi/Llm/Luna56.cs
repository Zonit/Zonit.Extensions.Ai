namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.6 Luna — the fast, low-cost tier of the GPT-5.6 family, for
/// cost-sensitive, high-volume work (summarization, drafting, routine
/// automation). Occupies the slot previously held by the <c>-nano</c> models.
/// See <see cref="Sol56"/> (frontier) and <see cref="Terra56"/> (balanced).
/// </summary>
/// <remarks>
/// 1.05M token context window; standard pricing applies up to 272K tokens,
/// input doubles beyond 272K. Wire alias <c>gpt-5.6-luna</c>.
/// </remarks>
public class Luna56 : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.6-luna";

    /// <inheritdoc />
    public override decimal PriceInput => 1.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 6.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.10m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.50m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 3.00m;

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
        ToolsType.FileSearch;

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

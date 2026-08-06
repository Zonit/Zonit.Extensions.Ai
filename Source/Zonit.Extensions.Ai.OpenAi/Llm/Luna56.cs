namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.6 Luna — the fast, low-cost tier of the GPT-5.6 family, for
/// cost-sensitive, high-volume work (summarization, drafting, routine
/// automation). Occupies the slot previously held by the <c>-nano</c> models.
/// See <see cref="Sol56"/> (frontier) and <see cref="Terra56"/> (balanced).
/// </summary>
/// <remarks>
/// 1.05M token context window; standard pricing applies up to 272K input
/// tokens, beyond which the long-context rates apply ($0.40 input / $0.04
/// cached / $1.80 output per 1M). Model id <c>gpt-5.6-luna</c>. Supports the full
/// reasoning range none / low / medium / high / <see cref="OpenAiReasonEffortExtended.Xhigh"/>
/// / <see cref="OpenAiReasonEffortExtended.Max"/>.
/// </remarks>
public class Luna56 : OpenAiReasoningBase<OpenAiReasonEffortExtended>, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.6-luna";

    /// <inheritdoc />
    public override decimal PriceInput => 0.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.20m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.02m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.10m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 0.60m;

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
    /// Context size past which OpenAI switches GPT-5.6 to long-context pricing
    /// for the remainder of the session (standard, batch and flex alike).
    /// </summary>
    private const long LongContextThreshold = 272_000;

    /// <summary>Input-side rates (input, cache read, cache write) double past the threshold.</summary>
    private const decimal LongContextInputMultiplier = 2m;

    /// <summary>Output-side rates rise by half past the threshold ($1.20 → $1.80).</summary>
    private const decimal LongContextOutputMultiplier = 1.5m;

    /// <inheritdoc />
    public override decimal GetInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold ? PriceInput * LongContextInputMultiplier : PriceInput;

    /// <inheritdoc />
    public override decimal GetOutputPrice(long inputTokens, long outputTokens)
        => inputTokens > LongContextThreshold ? PriceOutput * LongContextOutputMultiplier : PriceOutput;

    /// <inheritdoc />
    public override decimal GetCachedInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold
            ? PriceCachedInput!.Value * LongContextInputMultiplier
            : PriceCachedInput!.Value;

    /// <inheritdoc />
    public override decimal GetBatchInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold
            ? BatchPriceInput!.Value * LongContextInputMultiplier
            : BatchPriceInput!.Value;

    /// <inheritdoc />
    public override decimal GetBatchOutputPrice(long inputTokens, long outputTokens)
        => inputTokens > LongContextThreshold
            ? BatchPriceOutput!.Value * LongContextOutputMultiplier
            : BatchPriceOutput!.Value;
}

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.6 Terra — the balanced tier of the GPT-5.6 family, tuned for
/// high-volume business tasks (support, internal tools, document analysis)
/// where intelligence and cost must be balanced. Occupies the slot previously
/// held by the <c>-mini</c> models. See <see cref="Sol56"/> (frontier) and
/// <see cref="Luna56"/> (fast/low-cost).
/// </summary>
/// <remarks>
/// 1.05M token context window; standard pricing applies up to 272K input
/// tokens, beyond which the long-context rates apply ($4 input / $0.40 cached /
/// $18 output per 1M). Model id <c>gpt-5.6-terra</c>. Supports the full
/// reasoning range none / low / medium / high / <see cref="OpenAiReasonEffortExtended.Xhigh"/>
/// / <see cref="OpenAiReasonEffortExtended.Max"/>.
/// </remarks>
public class Terra56 : OpenAiReasoningBase<OpenAiReasonEffortExtended>, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.6-terra";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 12.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.20m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 1.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 6.00m;

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
    /// Context size past which OpenAI switches GPT-5.6 to long-context pricing
    /// for the remainder of the session (standard, batch and flex alike).
    /// </summary>
    private const long LongContextThreshold = 272_000;

    /// <summary>Input-side rates (input, cache read, cache write) double past the threshold.</summary>
    private const decimal LongContextInputMultiplier = 2m;

    /// <summary>Output-side rates rise by half past the threshold ($12 → $18).</summary>
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

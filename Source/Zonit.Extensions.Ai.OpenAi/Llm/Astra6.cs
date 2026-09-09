namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-6 Astra — OpenAI's most capable model, built for the hardest end-to-end
/// work: complex reasoning, coding, computer use, research and document
/// creation. Sits above the GPT-5.6 Sol / Terra / Luna tiers
/// (see <see cref="Sol56"/>), which continue on their own cadence.
/// </summary>
/// <remarks>
/// <para>
/// Model id <c>gpt-6-astra</c>. 1.05M token context window (922K max input),
/// 128K max output, knowledge cutoff 30 April 2026. Released 3 September 2026.
/// </para>
/// <para>
/// Standard pricing applies up to 272K input tokens, beyond which the
/// long-context rates apply ($20 input / $2 cached / $75 output per 1M).
/// Batch and flex run at half the applicable rate. Supports the full reasoning
/// range none / low / medium / high / <see cref="OpenAiReasonEffortExtended.Xhigh"/>
/// / <see cref="OpenAiReasonEffortExtended.Max"/>.
/// </para>
/// </remarks>
public class Astra6 : OpenAiReasoningBase<OpenAiReasonEffortExtended>, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-6-astra";

    /// <inheritdoc />
    public override decimal PriceInput => 10.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 50.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 1.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 25.00m;

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
    /// Context size past which OpenAI switches GPT-6 to long-context pricing
    /// for the remainder of the session (standard, batch and flex alike).
    /// </summary>
    private const long LongContextThreshold = 272_000;

    /// <summary>Input-side rates (input, cache read, cache write) double past the threshold.</summary>
    private const decimal LongContextInputMultiplier = 2m;

    /// <summary>Output-side rates rise by half past the threshold ($50 → $75).</summary>
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

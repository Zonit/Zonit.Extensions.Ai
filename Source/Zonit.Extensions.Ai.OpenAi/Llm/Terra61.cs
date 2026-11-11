namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-6.1 Terra — the balanced tier of the GPT-6 generation, for high-volume
/// business work (support, internal tools, document analysis) where intelligence
/// and cost must be balanced. Successor to <see cref="Terra56"/> at half its input
/// price. See <see cref="Sol61"/> (frontier), <see cref="Astra6"/> (flagship) and
/// <see cref="Luna6"/> (low cost).
/// </summary>
/// <remarks>
/// <para>
/// Model id <c>gpt-6.1-terra</c>. 1.05M token context window (922K max input),
/// 128K max output, knowledge cutoff 1 June 2026.
/// </para>
/// <para>
/// Standard pricing ($1 input / $0.05 cached / $1.25 cache write / $5 output per 1M)
/// applies up to 272K input tokens, beyond which the long-context rates apply ($2 /
/// $0.10 / $2.50 / $7.50). Batch and flex run at half the applicable rate.
/// </para>
/// <para>
/// Supports the full reasoning range none / low / medium (default) / high /
/// <see cref="OpenAiReasonEffortExtended.Xhigh"/> / <see cref="OpenAiReasonEffortExtended.Max"/>.
/// Fast mode at 2× the standard rate; no ultrafast tier.
/// </para>
/// </remarks>
public class Terra61 : OpenAiReasoningBase<OpenAiReasonEffortExtended>, IAgentLlm, IFast
{
    /// <inheritdoc />
    public override string Name => "gpt-6.1-terra";

    /// <inheritdoc />
    public override decimal PriceInput => 1.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 5.00m;

    /// <inheritdoc />
    /// <remarks>0.05× the uncached input rate.</remarks>
    public override decimal? PriceCachedInput => 0.05m;

    /// <inheritdoc />
    /// <remarks>1.25× the uncached input rate (30-minute retention); doubles with it past 272K input tokens.</remarks>
    public override decimal? PriceCachedInputWrite => 1.25m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.50m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 2.50m;

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
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Batch;

    /// <summary>
    /// Inference speed. Set to <see cref="SpeedType.Fast"/> to opt this request into OpenAI fast
    /// mode (<c>service_tier: "fast"</c>) — up to ~2.5× faster output and steadier latency, at
    /// double the standard rate on input, cached input and output. Defaults to
    /// <see cref="SpeedType.Standard"/>.
    /// </summary>
    /// <remarks>
    /// The premium lives in <see cref="IFast"/>'s defaults (a uniform 2×, applied on top of the
    /// long-context tiering below); whether it is charged is decided when the cost is computed,
    /// from the tier the response echoes back. Batch is unaffected.
    /// </remarks>
    public SpeedType Speed { get; init; } = SpeedType.Standard;

    /// <summary>
    /// Context size past which OpenAI switches GPT-6 to long-context pricing
    /// for the whole request (standard, batch and flex alike).
    /// </summary>
    private const long LongContextThreshold = 272_000;

    /// <summary>Input-side rates (input, cache read, cache write) double past the threshold.</summary>
    private const decimal LongContextInputMultiplier = 2m;

    /// <summary>Output-side rates rise by half past the threshold ($5 → $7.50).</summary>
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

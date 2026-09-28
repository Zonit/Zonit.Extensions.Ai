namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-6 Luna — the fast, low-cost tier of the GPT-6 generation, released
/// 22 September 2026, for cost-sensitive, high-volume work (summarization,
/// drafting, routine automation). Successor to <see cref="Luna56"/> at half its
/// price. See <see cref="Sol6"/> (frontier) and <see cref="Astra6"/> (flagship).
/// </summary>
/// <remarks>
/// <para>
/// Model id <c>gpt-6-luna</c>. 1.05M token context window, 128K max output,
/// knowledge cutoff 18 May 2026.
/// </para>
/// <para>
/// Standard pricing ($0.10 input / $0.01 cached / $0.50 output per 1M) applies
/// up to 272K input tokens, beyond which the long-context rates apply ($0.20 /
/// $0.02 / $0.75). Batch and flex run at half the applicable rate. Supports the
/// full reasoning range none / low / medium (default) / high /
/// <see cref="OpenAiReasonEffortExtended.Xhigh"/> / <see cref="OpenAiReasonEffortExtended.Max"/>.
/// Custom <c>temperature</c> / <c>top_p</c> are only accepted at effort
/// <c>none</c>.
/// </para>
/// </remarks>
public class Luna6 : OpenAiReasoningBase<OpenAiReasonEffortExtended>, IAgentLlm, IFast
{
    /// <inheritdoc />
    public override string Name => "gpt-6-luna";

    /// <inheritdoc />
    public override decimal PriceInput => 0.10m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.50m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.01m;

    /// <inheritdoc />
    /// <remarks>1.25× the uncached input rate; doubles with it past 272K input tokens.</remarks>
    public override decimal? PriceCachedInputWrite => 0.125m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 0.05m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 0.25m;

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
    /// Inference speed. Set to <see cref="SpeedType.Fast"/> to opt this request into OpenAI fast
    /// mode (<c>service_tier: "fast"</c>) — up to ~2.5× faster output and steadier latency, at
    /// double the standard rate on input, cached input and output. Defaults to
    /// <see cref="SpeedType.Standard"/>.
    /// </summary>
    /// <remarks>
    /// The premium itself lives in <see cref="IFast"/>'s defaults (a uniform 2×, applied on top of
    /// the long-context tiering below), and whether it is actually charged is decided when the
    /// cost is computed — OpenAI serves fast mode best-effort and only bills the premium when the
    /// response echoes the tier back. It is
    /// a synchronous-API tier only — Batch is unaffected, so the <c>GetBatch*</c> rates never
    /// carry it.
    /// </remarks>
    public SpeedType Speed { get; init; } = SpeedType.Standard;

    /// <summary>
    /// Context size past which OpenAI switches GPT-6 to long-context pricing
    /// for the remainder of the session (standard, batch and flex alike).
    /// </summary>
    private const long LongContextThreshold = 272_000;

    /// <summary>Input-side rates (input, cache read, cache write) double past the threshold.</summary>
    private const decimal LongContextInputMultiplier = 2m;

    /// <summary>Output-side rates rise by half past the threshold ($0.50 → $0.75).</summary>
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

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-6.2 Sol — OpenAI's model for everyday work, from coding to agents at scale, released
/// 17 November 2026. Successor to <see cref="Sol61"/> at the same rates. See
/// <see cref="Astra62"/> for the flagship, <see cref="Terra61"/> for the balanced tier and
/// <see cref="Luna6"/> for the low-cost tier.
/// </summary>
/// <remarks>
/// <para>
/// Model id <c>gpt-6.2-sol</c>. 1.05M token context window (922K max input),
/// 128K max output, knowledge cutoff 1 June 2026. Text and image in, text out.
/// </para>
/// <para>
/// Standard pricing ($2 input / $0.10 cached / $2.50 cache write / $10 output per 1M)
/// applies up to 272K input tokens, beyond which the long-context rates apply ($4 /
/// $0.20 / $5 / $15). Batch runs at half the applicable rate. No Flex processing. The cache
/// write rate is the default 30-minute lifetime; the 12-hour lifetime
/// (<c>prompt_cache_options.ttl: "12h"</c>, $5 per 1M) is not selected by this SDK.
/// </para>
/// <para>
/// The API rejects reasoning effort <c>none</c>, hence <see cref="OpenAiReasonEffortAlwaysOn"/>
/// (low / medium / high / xhigh / max; OpenAI recommends starting at medium). Custom
/// <c>temperature</c> / <c>top_p</c> are not accepted. On Chat Completions the model works
/// only without tool calling — this SDK uses the Responses API.
/// </para>
/// </remarks>
public class Sol62 : OpenAiReasoningBase<OpenAiReasonEffortAlwaysOn>, IAgentLlm, IFast, IUltraFast
{
    /// <inheritdoc />
    public override string Name => "gpt-6.2-sol";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.10m;

    /// <inheritdoc />
    /// <remarks>1.25× the uncached input rate; doubles with it past 272K input tokens.</remarks>
    public override decimal? PriceCachedInputWrite => 2.50m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 1.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 5.00m;

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
    /// mode (<c>service_tier: "fast"</c>) at double the standard rate on input, cached input and
    /// output. Set to <see cref="SpeedType.UltraFast"/> for the ultrafast tier
    /// (<c>service_tier: "ultrafast"</c>, US data residency only) — 3× the standard rate through
    /// 28 February 2027 ($6 / $0.30 cached / $7.50 cache write / $30 output per 1M), 6× from
    /// 1 March 2027 ($12 / $0.60 / $15 / $60). Defaults to <see cref="SpeedType.Standard"/>.
    /// </summary>
    /// <remarks>
    /// The premium lives in <see cref="IFast"/>'s and <see cref="IUltraFast"/>'s defaults, applied
    /// on top of the long-context tiering below, and whether it is charged is decided when the
    /// cost is computed from the tier the response echoes back. Batch is unaffected.
    /// </remarks>
    public SpeedType Speed { get; init; } = SpeedType.Standard;

    /// <summary>
    /// The ultrafast factor in force now: 3× during OpenAI's GPT-6.2 launch promotion (through
    /// 28 February 2027), 6× from 1 March 2027 (Pacific time). Follows the system clock.
    /// </summary>
    public decimal UltraFastMultiplier => Gpt62UltraFastPromotion.MultiplierAt(DateTimeOffset.UtcNow);

    /// <summary>
    /// Context size past which OpenAI switches GPT-6 to long-context pricing
    /// for the remainder of the session (standard and batch alike).
    /// </summary>
    private const long LongContextThreshold = 272_000;

    /// <summary>Input-side rates (input, cache read, cache write) double past the threshold.</summary>
    private const decimal LongContextInputMultiplier = 2m;

    /// <summary>Output-side rates rise by half past the threshold ($10 → $15).</summary>
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

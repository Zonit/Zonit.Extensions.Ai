namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-6.2 Astra — OpenAI's most capable model for the hardest end-to-end work, released
/// 17 November 2026. Successor to <see cref="Astra6"/>: input and output 20% cheaper, cached
/// input 60% cheaper. See <see cref="Sol62"/> for complex work at lower cost.
/// </summary>
/// <remarks>
/// <para>
/// Model id <c>gpt-6.2-astra</c>. 1.05M token context window (922K max input),
/// 128K max output, knowledge cutoff 18 May 2026. Text and image in, text out.
/// </para>
/// <para>
/// Standard pricing ($8 input / $0.40 cached / $10 cache write / $40 output per 1M)
/// applies up to 272K input tokens, beyond which the long-context rates apply ($16 /
/// $0.80 / $20 / $60). Batch runs at half the applicable rate. No Flex processing. The cache
/// write rate is the default 30-minute lifetime; the 12-hour lifetime
/// (<c>prompt_cache_options.ttl: "12h"</c>, $20 per 1M) is not selected by this SDK.
/// </para>
/// <para>
/// The API rejects reasoning effort <c>none</c>, hence <see cref="OpenAiReasonEffortAlwaysOn"/>
/// (low / medium / high / xhigh / max; OpenAI recommends starting at medium). Custom
/// <c>temperature</c> / <c>top_p</c> are not accepted and, unlike <see cref="Astra6"/>,
/// predicted outputs are not supported. On Chat Completions the model works only without tool
/// calling — this SDK uses the Responses API. OpenAI ships it with tighter safeguards on
/// higher-risk cybersecurity requests, which can also refuse some legitimate security testing.
/// </para>
/// </remarks>
public class Astra62 : OpenAiReasoningBase<OpenAiReasonEffortAlwaysOn>, IAgentLlm, IFast, IUltraFast
{
    /// <inheritdoc />
    public override string Name => "gpt-6.2-astra";

    /// <inheritdoc />
    public override decimal PriceInput => 8.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 40.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.40m;

    /// <inheritdoc />
    /// <remarks>1.25× the uncached input rate; doubles with it past 272K input tokens.</remarks>
    public override decimal? PriceCachedInputWrite => 10.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 4.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 20.00m;

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
    /// 28 February 2027 ($24 / $1.20 cached / $30 cache write / $120 output per 1M), 6× from
    /// 1 March 2027 ($48 / $2.40 / $60 / $240). Defaults to <see cref="SpeedType.Standard"/>.
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

    /// <summary>Output-side rates rise by half past the threshold ($40 → $60).</summary>
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

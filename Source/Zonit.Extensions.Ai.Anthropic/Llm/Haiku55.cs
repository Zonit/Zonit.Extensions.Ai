namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Haiku 5.5 - Successor to <see cref="Haiku45"/> and the current Haiku-tier
/// model, released 7 October 2026. The fast, low-cost model for high-volume work:
/// classification, routing, extraction, summarization and sub-agent tasks. The first
/// Haiku with adaptive thinking and effort levels (see <see cref="ReasonType"/>).
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window, 128K max output (300K on the Batch API behind the
/// <c>output-300k-2026-03-24</c> beta header). Newer tokenizer than Haiku 4.5: the same
/// text counts as about 30% more tokens. The minimum cacheable prompt drops to 512 tokens.
/// No fast mode.
/// </para>
/// <para>
/// <b>Pricing is tiered on prompt length</b> — the only current Claude model that is.
/// $0.10 input / $0.50 output per MTok when the prompt is 100K tokens or fewer, and
/// $0.50 / $2.50 above that; cache reads (0.1×) and writes (1.25× / 2×) follow the input
/// rate. The prompt length counts every input token, cache reads and writes included, and
/// a request over the threshold pays the higher rates on all of its tokens
/// (see <see cref="GetInputPrice"/>).
/// </para>
/// <para>
/// <b>Thinking is ON by default on the wire.</b> Like <see cref="Opus5"/>, the model thinks
/// adaptively when the <c>thinking</c> field is omitted. This class opts out via
/// <see cref="ThinkingEnabledByDefault"/>, so leaving
/// <see cref="AnthropicReasoningBase{TReason}.Reason"/> <c>null</c> sends an explicit
/// <c>thinking: { "type": "disabled" }</c> — "Reason not set" keeps meaning "no thinking".
/// That is safe because Anthropic accepts a disabled <c>thinking</c> at effort <c>high</c>
/// or below, and a request with no <c>Reason</c> sends no <c>output_config.effort</c>
/// (server default <c>medium</c>). With thinking off the model can skip a needed tool call
/// when structured output is requested too — prefer <see cref="ReasonType.Low"/> on
/// tool-calling workloads.
/// </para>
/// <para>
/// Non-default <c>temperature</c> / <c>top_p</c> / <c>top_k</c> and assistant prefill are
/// rejected with a 400. Forced <c>tool_choice</c> is accepted (the call then skips
/// thinking). Safety classifiers can return <c>stop_reason: "refusal"</c>, and this model
/// has no server-side fallback.
/// </para>
/// </remarks>
public class Haiku55 : AnthropicFixedSamplingBase<Haiku55.ReasonType>, IAgentLlm
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Haiku 5.5. Numeric values
    /// match <see cref="ReasoningEffort"/> exactly.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>No thinking — model responds directly.</summary>
        None = 0,
        /// <summary>Light reasoning — fastest, lowest cost. Suited to chat and simple high-volume requests.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth. The server default when thinking is on and no effort is sent.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning — knowledge work and strict instruction following.</summary>
        High = 3,
        /// <summary>Extra effort — above <see cref="High"/> but below <see cref="Max"/>. Wire value <c>"xhigh"</c>.</summary>
        Extra = 4,
        /// <summary>Maximum thinking budget — slowest, highest accuracy.</summary>
        Max = 5,
    }

    /// <summary>Prompt size past which Anthropic bills the whole request at the higher rate card.</summary>
    private const long LongPromptThreshold = 100_000;

    /// <summary>Haiku 5.5's higher rate card is exactly 5× the standard one on every rate.</summary>
    private const decimal LongPromptMultiplier = 5m;

    /// <inheritdoc />
    public override string Name => "claude-haiku-5-5";

    /// <inheritdoc />
    /// <remarks>Up to 100K prompt tokens; $0.50 above (see <see cref="GetInputPrice"/>).</remarks>
    public override decimal PriceInput => 0.10m;

    /// <inheritdoc />
    /// <remarks>Up to 100K prompt tokens; $2.50 above.</remarks>
    public override decimal PriceOutput => 0.50m;

    /// <inheritdoc />
    /// <remarks>5-minute TTL (1.25× input). The 1-hour TTL is $0.20 (2× input), derived by the base.</remarks>
    public override decimal PriceCachedWrite => 0.125m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.01m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_000_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 128_000;

    /// <inheritdoc />
    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output { get; } = ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.MCP;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.Reasoning;

    /// <inheritdoc />
    protected internal override bool ThinkingEnabledByDefault => true;

    /// <summary>$0.10 up to 100K prompt tokens, $0.50 above.</summary>
    public override decimal GetInputPrice(long inputTokens)
        => inputTokens > LongPromptThreshold ? PriceInput * LongPromptMultiplier : PriceInput;

    /// <summary>$0.50 up to 100K prompt tokens, $2.50 above — keyed on the prompt size.</summary>
    public override decimal GetOutputPrice(long inputTokens, long outputTokens)
        => inputTokens > LongPromptThreshold ? PriceOutput * LongPromptMultiplier : PriceOutput;

    /// <summary>$0.01 up to 100K prompt tokens, $0.05 above.</summary>
    public override decimal GetCachedInputPrice(long inputTokens)
        => inputTokens > LongPromptThreshold ? PriceCachedRead * LongPromptMultiplier : PriceCachedRead;

    /// <summary>The selected TTL's write rate ($0.125 at 5 minutes, $0.20 at 1 hour), ×5 above 100K prompt tokens.</summary>
    public override decimal GetCachedInputWritePrice(long inputTokens)
        => inputTokens > LongPromptThreshold
            ? base.GetCachedInputWritePrice(inputTokens) * LongPromptMultiplier
            : base.GetCachedInputWritePrice(inputTokens);
}

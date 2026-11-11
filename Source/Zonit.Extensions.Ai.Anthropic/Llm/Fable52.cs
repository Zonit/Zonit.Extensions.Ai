namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Fable 5.2 — Anthropic's most capable widely released model, for the
/// most demanding reasoning and long-horizon agentic work. Successor to
/// <see cref="Fable51"/> in the same tier at a lower price ($8 / $40 per MTok
/// instead of $10 / $50). Supports adaptive thinking with five effort levels
/// (see <see cref="ReasonType"/>).
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window at standard pricing (no long-context surcharge),
/// 128K max output. Cache reads $0.20/1M (0.025× input); cache writes $10 at the
/// 5-minute TTL and $16 at the 1-hour TTL. Batch is half the standard card.
/// </para>
/// <para>
/// Thinking is always on server-side; the legacy <c>budget_tokens</c> mode and
/// an explicit <c>thinking: disabled</c> are both rejected, so leaving
/// <see cref="AnthropicReasoningBase{TReason}.Reason"/> <c>null</c> omits the
/// field and the server thinks at its default effort (<c>high</c>). Forced
/// <c>tool_choice</c> is rejected as well, so structured output goes through
/// <c>auto</c> plus an instruction (see <see cref="AnthropicBase.SupportsForcedToolChoice"/>).
/// </para>
/// <para>
/// Changes from Fable 5.1 that matter to callers: the history-editing check on
/// replayed thinking blocks is enforced for every account (this SDK replays them
/// unchanged), no earlier model reads its thinking blocks, and fast mode is
/// available at $16 / $80 per MTok (see <see cref="Speed"/>). No Priority Tier.
/// </para>
/// <para>
/// Requires 30-day data retention — a zero-data-retention organisation gets a
/// 400 unless Anthropic has expressly authorised it.
/// </para>
/// </remarks>
public class Fable52 : AnthropicFixedSamplingBase<Fable52.ReasonType>, IAgentLlm, IFast
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Fable 5.2. Numeric
    /// values match <see cref="ReasoningEffort"/> exactly, including the
    /// <see cref="Extra"/> slot.
    /// There is no <c>None</c> slot — thinking is always on for this model and an
    /// explicit <c>thinking: disabled</c> is rejected with a 400.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>Light reasoning — fastest, lowest cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning. The server default when <c>Reason</c> is not set.</summary>
        High = 3,
        /// <summary>Extra effort — above <see cref="High"/> but below <see cref="Max"/>. Wire value <c>"xhigh"</c>.</summary>
        Extra = 4,
        /// <summary>Maximum thinking budget — slowest, highest accuracy.</summary>
        Max = 5,
    }

    /// <inheritdoc />
    public override string Name => "claude-fable-5-2";

    /// <inheritdoc />
    public override decimal PriceInput => 8.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 40.00m;

    /// <inheritdoc />
    /// <remarks>5-minute TTL (1.25× input). The 1-hour TTL is $16 (2× input), derived by the base.</remarks>
    public override decimal PriceCachedWrite => 10.00m;

    /// <inheritdoc />
    /// <remarks>0.025× input.</remarks>
    public override decimal PriceCachedRead => 0.20m;

    /// <summary>
    /// Inference speed. Set to <see cref="SpeedType.Fast"/> to opt this request into
    /// fast mode (up to ~2.5× output tokens/sec) at $16 / $80 per MTok. Messages API on
    /// the Claude API only — not on Batch. Defaults to <see cref="SpeedType.Standard"/>.
    /// </summary>
    public SpeedType Speed { get; init; } = SpeedType.Standard;

    /// <inheritdoc />
    /// <remarks>
    /// $16 / $80 per MTok — Anthropic's flat 2× on the standard card, which is exactly what
    /// <see cref="IFast"/> assumes by default; stated explicitly so the fast rates are visible
    /// on the model. Whether they are charged is decided when the cost is computed.
    /// </remarks>
    public decimal FastPriceInput => 16.00m;

    /// <inheritdoc cref="FastPriceInput" />
    public decimal FastPriceOutput => 80.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_000_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 128_000;

    /// <inheritdoc />
    public override ChannelType Input { get; } = ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output { get; } = ChannelType.Text;

    /// <inheritdoc />
    protected internal override bool SupportsForcedToolChoice => false;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.MCP;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.Reasoning;
}

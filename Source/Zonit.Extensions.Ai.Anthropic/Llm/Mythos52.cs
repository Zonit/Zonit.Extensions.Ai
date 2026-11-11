namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Mythos 5.2 — the verified-organization counterpart of
/// <see cref="Fable52"/>: same model, capabilities, pricing and API surface,
/// available only to organizations verified through Anthropic's verification
/// programs (such as the Cyber Verification Program). Successor to
/// <see cref="Mythos51"/>. Supports adaptive thinking with five effort levels
/// (see <see cref="ReasonType"/>).
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window at standard pricing, 128K max output. $8 / $40 per
/// MTok; cache reads $0.20 (0.025× input); cache writes $10 (5 min) / $16 (1 h).
/// Fast mode $16 / $80 per MTok. Requires verified access on the Anthropic
/// account and 30-day data retention; shares one rate limit with the earlier
/// Mythos models.
/// </para>
/// <para>
/// Thinking is always on server-side; the legacy <c>budget_tokens</c> mode,
/// an explicit <c>thinking: disabled</c> and forced <c>tool_choice</c> are all
/// rejected — see <see cref="AnthropicBase.SupportsForcedToolChoice"/>. The
/// history-editing check on replayed thinking blocks is enforced for every
/// account. Runs access-program safeguards, so a <c>refusal</c> stop reason can
/// occur.
/// </para>
/// </remarks>
public class Mythos52 : AnthropicFixedSamplingBase<Mythos52.ReasonType>, IAgentLlm, IFast
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Mythos 5.2. Numeric
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
    public override string Name => "claude-mythos-5-2";

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
    /// fast mode at $16 / $80 per MTok. Not on Batch. Defaults to <see cref="SpeedType.Standard"/>.
    /// </summary>
    public SpeedType Speed { get; init; } = SpeedType.Standard;

    /// <inheritdoc />
    /// <remarks>$16 / $80 per MTok — a flat 2× on the standard card.</remarks>
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

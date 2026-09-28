namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Opus 5.5 - Successor to <see cref="Opus5"/> and the current Opus-tier
/// model, released 22 September 2026. Cheaper than Opus 5 on every rate
/// ($4 / $20 per MTok instead of $5 / $25) with an unusually low cache-read rate
/// (0.05× input). Supports adaptive thinking with five effort levels
/// (see <see cref="ReasonType"/>), including <see cref="ReasonType.Extra"/>.
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window at standard pricing (no surcharge for long context),
/// 128K max output (300K on the Batch API behind the <c>output-300k-2026-03-24</c>
/// beta header). Knowledge cutoff June 2026.
/// </para>
/// <para>
/// <b>Thinking is always on.</b> Like <see cref="Fable51"/>, the model rejects both
/// an explicit <c>thinking: { "type": "disabled" }</c> and the legacy
/// <c>budget_tokens</c> mode with a 400, so there is no <c>None</c> level here and
/// leaving <see cref="AnthropicReasoningBase{TReason}.Reason"/> <c>null</c> omits
/// the <c>thinking</c> field — the server then thinks adaptively at its default
/// effort, which for Opus 5.5 is <c>medium</c> (not <c>high</c> as on the other
/// current models). Anthropic recommends <c>max_tokens</c> of at least 64K at
/// <see cref="ReasonType.Extra"/> / <see cref="ReasonType.Max"/>; the provider
/// already grants the full output capacity at those levels.
/// </para>
/// <para>
/// Forced <c>tool_choice</c> (<c>any</c> / <c>tool</c>) is rejected, so structured
/// output goes through <c>auto</c> plus an instruction (see
/// <see cref="AnthropicBase.SupportsForcedToolChoice"/>). Non-default
/// <c>temperature</c> / <c>top_p</c> / <c>top_k</c> and assistant prefill are
/// rejected too.
/// </para>
/// </remarks>
public class Opus55 : AnthropicReasoningBase<Opus55.ReasonType>, IAgentLlm, IFast
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Opus 5.5. Numeric
    /// values match <see cref="ReasoningEffort"/> exactly. There is no
    /// <c>None</c> slot — thinking cannot be switched off on this model.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>Light reasoning — fastest, lowest cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth. The server default when <c>Reason</c> is not set.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning.</summary>
        High = 3,
        /// <summary>Extra effort — above <see cref="High"/> but below <see cref="Max"/>. Wire value <c>"xhigh"</c>.</summary>
        Extra = 4,
        /// <summary>Maximum thinking budget — slowest, highest accuracy.</summary>
        Max = 5,
    }

    /// <inheritdoc />
    public override string Name => "claude-opus-5-5";

    /// <inheritdoc />
    public override decimal PriceInput => 4.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 20.00m;

    /// <inheritdoc />
    /// <remarks>5-minute TTL (1.25× input). The 1-hour TTL is $8 (2× input), derived by the base.</remarks>
    public override decimal PriceCachedWrite => 5.00m;

    /// <inheritdoc />
    /// <remarks>0.05× input — half the usual 0.1× cache-read ratio.</remarks>
    public override decimal PriceCachedRead => 0.20m;

    /// <summary>
    /// Inference speed. Set to <see cref="SpeedType.Fast"/> to opt this request
    /// into fast mode (up to ~2.5× output tokens/sec) at the premium fast
    /// pricing below. Requires fast-mode access on your Anthropic account
    /// (research preview, first-party API only, not available on Batch).
    /// Defaults to <see cref="SpeedType.Standard"/>.
    /// </summary>
    public SpeedType Speed { get; init; } = SpeedType.Standard;

    /// <inheritdoc />
    /// <remarks>
    /// $8 / $40 per MTok — Anthropic's flat 2× on the standard card, which is exactly what
    /// <see cref="IFast"/> assumes by default; stated explicitly here so the fast rates are
    /// visible on the model. Whether they are charged is decided when the cost is computed.
    /// </remarks>
    public decimal FastPriceInput => 8.00m;

    /// <inheritdoc cref="FastPriceInput" />
    public decimal FastPriceOutput => 40.00m;

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

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Sonnet 5 - The best combination of speed and intelligence; successor
/// to <see cref="Sonnet46"/>. Supports adaptive thinking with five effort
/// levels (see <see cref="ReasonType"/>), including <see cref="ReasonType.Extra"/>
/// — an Opus-tier capability Sonnet 4.6 did not expose.
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window at standard pricing (no surcharge for long context).
/// 128K max output tokens — double Sonnet 4.6's 64K ceiling. Adaptive thinking
/// only — does not accept the legacy <c>budget_tokens</c> mode, and rejects
/// non-default <c>temperature</c> / <c>top_p</c> / <c>top_k</c> with a 400.
/// </para>
/// <para>
/// <b>Thinking is ON by default on the wire.</b> Unlike every other model in
/// this SDK, Claude Sonnet 5 enables adaptive thinking even when the
/// <c>thinking</c> field is omitted from the request entirely. This class
/// opts out via <see cref="ThinkingEnabledByDefault"/> so the provider sends
/// an explicit <c>thinking: { "type": "disabled" }</c> whenever
/// <see cref="AnthropicReasoningBase{TReason}.Reason"/> is left <c>null</c> —
/// keeping "Reason not set" meaning "no thinking", consistent with
/// <see cref="Sonnet46"/> and every other reasoning model here.
/// </para>
/// <para>
/// Uses the newer tokenizer introduced with Opus 4.7 — produces roughly 30%
/// more tokens than Sonnet 4.6 for the same text. Re-baseline token counts
/// and <see cref="ILlm.MaxOutputTokens"/> headroom rather than reusing
/// Sonnet 4.6 figures.
/// </para>
/// <para>
/// Pricing below is the <b>standard</b> rate, effective September 1, 2026.
/// Anthropic offers introductory pricing of $2 / $10 per MTok (input / output)
/// — with proportionally discounted cache rates — through August 31, 2026.
/// </para>
/// </remarks>
public class Sonnet5 : AnthropicReasoningBase<Sonnet5.ReasonType>, IAgentLlm
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Sonnet 5. Numeric
    /// values match <see cref="ReasoningEffort"/> exactly, including the
    /// <see cref="Extra"/> slot.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>No thinking — model responds directly.</summary>
        None = 0,
        /// <summary>Light reasoning — fastest, lowest cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning.</summary>
        High = 3,
        /// <summary>Extra effort — above <see cref="High"/> but below <see cref="Max"/>. Wire value <c>"xhigh"</c>.</summary>
        Extra = 4,
        /// <summary>Maximum thinking budget — slowest, highest accuracy.</summary>
        Max = 5,
    }

    /// <inheritdoc />
    public override string Name => "claude-sonnet-5";

    /// <inheritdoc />
    public override decimal PriceInput => 3.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 15.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 3.75m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.30m;

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
}

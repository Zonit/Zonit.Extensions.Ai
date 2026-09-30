namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Sonnet 5.5 - Successor to <see cref="Sonnet5"/> and the current Sonnet-tier
/// model, released 28 September 2026. Same $2 / $10 per MTok pricing as Sonnet 5.
/// Supports adaptive thinking with five effort levels (see <see cref="ReasonType"/>),
/// including <see cref="ReasonType.Extra"/>.
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window at standard pricing (no long-context surcharge), 128K max
/// output (300K on the Batch API behind the <c>output-300k-2026-03-24</c> beta header).
/// Knowledge cutoff June 2026. Same tokenizer as Sonnet 5; the minimum cacheable
/// prompt drops to 512 tokens. No fast mode.
/// </para>
/// <para>
/// <b>Thinking cannot be disabled.</b> Unlike <see cref="Sonnet5"/>, an explicit
/// <c>thinking: { "type": "disabled" }</c> (and the legacy <c>budget_tokens</c> mode)
/// is rejected with a 400, so there is no <c>None</c> level here and leaving
/// <see cref="AnthropicReasoningBase{TReason}.Reason"/> <c>null</c> omits the
/// <c>thinking</c> field — the server then thinks adaptively at its default effort,
/// <c>high</c>.
/// </para>
/// <para>
/// Forced <c>tool_choice</c> (<c>any</c> / <c>tool</c>) is rejected, so structured
/// output goes through <c>auto</c> plus an instruction (see
/// <see cref="AnthropicBase.SupportsForcedToolChoice"/>). Non-default
/// <c>temperature</c> / <c>top_p</c> / <c>top_k</c> and assistant prefill are rejected
/// too.
/// </para>
/// </remarks>
public class Sonnet55 : AnthropicReasoningBase<Sonnet55.ReasonType>, IAgentLlm
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Sonnet 5.5. Numeric values
    /// match <see cref="ReasoningEffort"/> exactly. There is no <c>None</c> slot —
    /// thinking cannot be switched off on this model.
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
    public override string Name => "claude-sonnet-5-5";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;

    /// <inheritdoc />
    /// <remarks>5-minute TTL (1.25× input). The 1-hour TTL is $4 (2× input), derived by the base.</remarks>
    public override decimal PriceCachedWrite => 2.50m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.20m;

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

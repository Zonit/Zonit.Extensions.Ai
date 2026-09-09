namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Mythos 5.1 — the Project Glasswing counterpart of
/// <see cref="Fable51"/>: same capabilities, pricing and API surface, available
/// by invitation only. Successor to <see cref="Mythos5"/>.
/// Supports adaptive thinking with five effort levels (see <see cref="ReasonType"/>).
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window at standard pricing, 128K max output. Requires
/// Project Glasswing access on the Anthropic account, and 30-day data
/// retention.
/// </para>
/// <para>
/// Thinking is always on server-side; the legacy <c>budget_tokens</c> mode and
/// forced <c>tool_choice</c> are both rejected — see
/// <see cref="AnthropicBase.SupportsForcedToolChoice"/>.
/// </para>
/// <para>
/// Cache reads are billed at the Mythos 5 rate of $1.00/1M here. Fable 5.1
/// dropped to $0.25/1M; Anthropic has not confirmed whether Mythos 5.1 shares
/// that rate, so the higher figure is used rather than under-reporting cost.
/// </para>
/// </remarks>
public class Mythos51 : AnthropicReasoningBase<Mythos51.ReasonType>, IAgentLlm
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Mythos 5.1. Numeric
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
    public override string Name => "claude-mythos-5-1";

    /// <inheritdoc />
    public override decimal PriceInput => 10.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 50.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 12.50m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 1.00m;

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

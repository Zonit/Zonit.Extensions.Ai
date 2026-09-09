namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Fable 5.1 — Anthropic's most capable widely released model, for the
/// most demanding reasoning and long-horizon agentic work. Successor to
/// <see cref="Fable5"/> in the same tier and at the same per-token price.
/// Supports adaptive thinking with five effort levels (see <see cref="ReasonType"/>).
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window at standard pricing (no long-context surcharge),
/// 128K max output. Cache reads are $0.25/1M — a quarter of the Fable 5 rate.
/// </para>
/// <para>
/// Thinking is always on server-side; the legacy <c>budget_tokens</c> mode and
/// an explicit <c>thinking: disabled</c> are both rejected. Forced
/// <c>tool_choice</c> is rejected as well, so structured output goes through
/// <c>auto</c> plus an instruction (see
/// <see cref="AnthropicBase.SupportsForcedToolChoice"/>).
/// </para>
/// <para>
/// Requires 30-day data retention — a zero-data-retention organisation gets a
/// 400 unless Anthropic has expressly authorised it.
/// </para>
/// </remarks>
public class Fable51 : AnthropicReasoningBase<Fable51.ReasonType>, IAgentLlm
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Fable 5.1. Numeric
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
    public override string Name => "claude-fable-5-1";

    /// <inheritdoc />
    public override decimal PriceInput => 10.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 50.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 12.50m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.25m;

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

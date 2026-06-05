namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Opus 4.6 — flagship model with adaptive thinking via the
/// <c>effort</c> parameter. See <see cref="ReasonType"/> for the levels
/// this model accepts (the <c>Extra</c> level is <b>not</b> supported
/// — it is exclusive to Opus 4.7+).
/// </summary>
/// <remarks>
/// <para>
/// 1M token context window at standard pricing.
/// </para>
/// <para>
/// Migrated from the legacy <c>thinking.type = "enabled"</c> + <c>budget_tokens</c>
/// payload to <c>thinking.type = "adaptive"</c> + <c>output_config.effort</c>
/// per Anthropic's deprecation notice: <i>"thinking.type: 'enabled' and
/// budget_tokens are deprecated on Opus 4.6 and Sonnet 4.6 and will be removed
/// in a future model release. Use thinking.type: 'adaptive' with the effort
/// parameter instead."</i> Adaptive thinking also automatically enables
/// interleaved thinking on Opus 4.6, which is required for the agentic
/// workflows this SDK targets (manual mode does not support interleaved
/// thinking on Opus 4.6).
/// </para>
/// <para>
/// Consider migrating to Claude Opus 4.7 for improved intelligence and the
/// additional <c>Extra</c> effort level.
/// </para>
/// </remarks>
/// <seealso href="https://platform.claude.com/docs/en/build-with-claude/adaptive-thinking">Adaptive thinking — Supported models</seealso>
[Obsolete("Claude Opus 4.6 is being phased out — migrate to Opus48 (claude-opus-4-8). Still functional, but Anthropic will retire older models.")]
public class Opus46 : AnthropicReasoningBase<Opus46.ReasonType>, IAgentLlm
{
    /// <summary>
    /// Adaptive-thinking effort levels accepted by Claude Opus 4.6. Numeric
    /// values intentionally match <see cref="ReasoningEffort"/> — the
    /// <c>Extra</c> slot (<c>4</c>) is skipped because Opus 4.6 rejects it
    /// (Extra is Opus 4.7+ only).
    /// </summary>
    public enum ReasonType
    {
        /// <summary>No thinking — model responds directly.</summary>
        None = 0,
        /// <summary>Light reasoning — fastest, lowest cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning. At <c>High</c> and <see cref="Max"/> effort Opus 4.6 almost always thinks deeply.</summary>
        High = 3,
        /// <summary>Maximum thinking budget — slowest, highest accuracy.</summary>
        Max = 5,
    }

    /// <inheritdoc />
    public override string Name => "claude-opus-4-6";

    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 25.00m;

    /// <inheritdoc />
    public override decimal PriceCachedWrite => 6.25m;

    /// <inheritdoc />
    public override decimal PriceCachedRead => 0.50m;

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
}

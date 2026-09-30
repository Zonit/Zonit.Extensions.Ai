namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 3.8 Flash — Google's newest generally available Gemini model (GA
/// 2 September 2026) and the recommended default for most workloads. There is no
/// generally available Gemini 3.x Pro; see <see cref="Gemini31Pro"/> for the preview.
/// </summary>
/// <remarks>
/// <para>
/// 1,048,576 input / 65,536 output tokens; text, image, video, audio and PDF in.
/// </para>
/// <para>
/// Launch pricing: $0.75 input / $0.075 cached / $3.75 output per 1M tokens until
/// 31 December 2026, then <b>$1.50 / $0.15 / $7.50</b> from 1 January 2027 (UTC).
/// The prices follow the system clock and switch on their own. Batch and Flex run at
/// half the applicable rate.
/// </para>
/// <para>
/// Thinking levels <c>low</c> / <c>medium</c> / <c>high</c> — <c>minimal</c> is
/// rejected on this model, unlike <see cref="Gemini35Flash"/>.
/// </para>
/// </remarks>
public class Gemini38Flash : GoogleThinkingBase<Gemini38Flash.ReasonType>
{
    /// <summary>
    /// Thinking levels accepted by <c>gemini-3.8-flash</c> (<c>low</c> / <c>medium</c> / <c>high</c>; default <c>medium</c>).
    /// Numeric values align with <see cref="ReasoningEffort"/>.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>Light reasoning — lower latency and cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning.</summary>
        High = 3,
    }

    /// <inheritdoc />
    public override string Name => "gemini-3.8-flash";

    /// <summary>First moment (UTC) the post-launch rate card applies.</summary>
    private static readonly DateTimeOffset StandardPricingFrom = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    /// <remarks>$0.75 until 31 December 2026, $1.50 from 1 January 2027.</remarks>
    public override decimal PriceInput => RateCard(DateTimeOffset.UtcNow).Input;

    /// <inheritdoc />
    /// <remarks>$3.75 until 31 December 2026, $7.50 from 1 January 2027.</remarks>
    public override decimal PriceOutput => RateCard(DateTimeOffset.UtcNow).Output;

    /// <inheritdoc />
    /// <remarks>$0.075 until 31 December 2026, $0.15 from 1 January 2027.</remarks>
    public override decimal? PriceCachedInput => RateCard(DateTimeOffset.UtcNow).CachedInput;

    /// <summary>
    /// Google's rate card in force at <paramref name="at"/>: the launch rates through
    /// 31 December 2026, double from 1 January 2027 (compared in UTC). The prices above
    /// resolve it against the system clock, so a cost computed in 2027 bills the new
    /// rates without a package update.
    /// </summary>
    internal static (decimal Input, decimal CachedInput, decimal Output) RateCard(DateTimeOffset at)
        => at < StandardPricingFrom
            ? (0.75m, 0.075m, 3.75m)
            : (1.50m, 0.15m, 7.50m);

    /// <inheritdoc />
    public override int MaxInputTokens => 1_048_576;

    /// <inheritdoc />
    public override int MaxOutputTokens => 65_536;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image | ChannelType.Audio | ChannelType.Video;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.CodeInterpreter;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.Reasoning;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

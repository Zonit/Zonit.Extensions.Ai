namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Shared surface of the Gemini 4 generation — <see cref="Gemini4Carbon"/> (deepest
/// reasoning) and <see cref="Gemini4Argon"/> (faster, fewer tokens per turn). Both
/// take the same parameters and the same rate card.
/// </summary>
/// <remarks>
/// <para>
/// 1,048,576-token context window shared between input and output; 65,536 output
/// tokens on synchronous requests (Google's long-decode mode, up to the full window,
/// is not wrapped by this SDK). Text, image, video, audio and PDF in; text out.
/// </para>
/// <para>
/// Introductory pricing: $2.00 input / $0.10 cached / $10.00 output per 1M tokens
/// through 30 November 2026, then <b>$4.00 / $0.20 / $20.00</b> from 1 December 2026
/// (UTC). The prices follow the system clock and switch on their own. Output includes
/// thinking tokens. Batch and Flex run at half the applicable rate. No separate
/// long-context tier. Context caching is implicit only.
/// </para>
/// <para>
/// Thinking levels <c>low</c> / <c>medium</c> (default) / <c>high</c>; <c>minimal</c>
/// is not offered.
/// </para>
/// </remarks>
public abstract class Gemini4Base : GoogleThinkingBase<Gemini4Base.ReasonType>
{
    /// <summary>
    /// Thinking levels accepted by the Gemini 4 models (<c>low</c> / <c>medium</c> / <c>high</c>; default <c>medium</c>).
    /// Numeric values align with <see cref="ReasoningEffort"/>.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>Light reasoning — lower latency and cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth. The server default.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning.</summary>
        High = 3,
    }

    /// <summary>First moment (UTC) the post-introductory rate card applies.</summary>
    private static readonly DateTimeOffset StandardPricingFrom = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    /// <remarks>$2.00 through 30 November 2026, $4.00 from 1 December 2026.</remarks>
    public override decimal PriceInput => RateCard(DateTimeOffset.UtcNow).Input;

    /// <inheritdoc />
    /// <remarks>$10.00 through 30 November 2026, $20.00 from 1 December 2026.</remarks>
    public override decimal PriceOutput => RateCard(DateTimeOffset.UtcNow).Output;

    /// <inheritdoc />
    /// <remarks>$0.10 through 30 November 2026, $0.20 from 1 December 2026 (95% off input).</remarks>
    public override decimal? PriceCachedInput => RateCard(DateTimeOffset.UtcNow).CachedInput;

    /// <summary>
    /// Google's rate card in force at <paramref name="at"/>: the introductory rates through
    /// 30 November 2026, double from 1 December 2026 (compared in UTC).
    /// </summary>
    internal static (decimal Input, decimal CachedInput, decimal Output) RateCard(DateTimeOffset at)
        => at < StandardPricingFrom
            ? (2.00m, 0.10m, 10.00m)
            : (4.00m, 0.20m, 20.00m);

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

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
/// 31 December 2026, then <b>$1.50 / $0.15 / $7.50</b> from 1 January 2027 — the
/// rates here are the launch ones, so bump them (or the package) once the
/// promotion ends. Batch and Flex run at half price.
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

    /// <inheritdoc />
    public override decimal PriceInput => 0.75m;

    /// <inheritdoc />
    public override decimal PriceOutput => 3.75m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.075m;

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

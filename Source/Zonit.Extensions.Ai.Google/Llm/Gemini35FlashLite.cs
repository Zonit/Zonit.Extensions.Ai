namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 3.5 Flash-Lite — Google's cost-efficient, low-latency model for
/// high-volume work (GA 21 July 2026). Successor to <see cref="Gemini31FlashLite"/>.
/// </summary>
/// <remarks>
/// <para>
/// 1,048,576 input / 65,536 output tokens. $0.30 input (every modality) / $0.03
/// cached / $2.50 output per 1M tokens; Batch and Flex at half price.
/// </para>
/// <para>
/// Thinking levels <c>minimal</c> (default) / <c>low</c> / <c>medium</c> / <c>high</c>.
/// </para>
/// </remarks>
public class Gemini35FlashLite : GoogleThinkingBase<Gemini35FlashLite.ReasonType>
{
    /// <summary>
    /// Thinking levels accepted by <c>gemini-3.5-flash-lite</c> (<c>minimal</c> / <c>low</c> / <c>medium</c> / <c>high</c>; default <c>minimal</c>).
    /// Numeric values align with <see cref="ReasoningEffort"/>.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>Lowest thinking level — closest to "no thinking" (Gemini 3.x cannot switch it off). Wire value <c>"minimal"</c>.</summary>
        Minimal = 0,
        /// <summary>Light reasoning — lower latency and cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning.</summary>
        High = 3,
    }

    /// <inheritdoc />
    public override string Name => "gemini-3.5-flash-lite";

    /// <inheritdoc />
    public override decimal PriceInput => 0.30m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.50m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.03m;

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

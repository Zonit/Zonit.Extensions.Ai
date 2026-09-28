namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 3.1 Flash-Lite — the cheapest Gemini 3.x model (GA 7 May 2026). Google
/// shuts it down on 7 May 2027 in favour of <see cref="Gemini35FlashLite"/>.
/// </summary>
/// <remarks>
/// <para>
/// 1,048,576 input / 65,536 output tokens. $0.25 input / $0.025 cached / $1.50
/// output per 1M tokens (audio input is billed at $0.50 / $0.05 cached, which this
/// flat rate does not model); Batch and Flex at half price.
/// </para>
/// <para>
/// Thinking levels <c>minimal</c> (default) / <c>low</c> / <c>medium</c> / <c>high</c>.
/// </para>
/// </remarks>
public class Gemini31FlashLite : GoogleThinkingBase<Gemini31FlashLite.ReasonType>
{
    /// <summary>
    /// Thinking levels accepted by <c>gemini-3.1-flash-lite</c> (<c>minimal</c> / <c>low</c> / <c>medium</c> / <c>high</c>; default <c>minimal</c>).
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
    public override string Name => "gemini-3.1-flash-lite";

    /// <inheritdoc />
    public override decimal PriceInput => 0.25m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.50m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.025m;

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

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 3.5 Flash — the first Gemini 3.5 model (GA 19 May 2026) and the model
/// behind the <c>gemini-flash-latest</c> alias. Prefer <see cref="Gemini38Flash"/>,
/// which is newer and cheaper while its launch pricing lasts.
/// </summary>
/// <remarks>
/// <para>
/// 1,048,576 input / 65,536 output tokens. $1.50 input / $0.15 cached / $9.00
/// output per 1M tokens; Batch and Flex at half price.
/// </para>
/// <para>
/// Thinking levels <c>minimal</c> / <c>low</c> / <c>medium</c> (default) / <c>high</c>.
/// </para>
/// </remarks>
public class Gemini35Flash : GoogleThinkingBase<Gemini35Flash.ReasonType>
{
    /// <summary>
    /// Thinking levels accepted by <c>gemini-3.5-flash</c> (<c>minimal</c> / <c>low</c> / <c>medium</c> / <c>high</c>; default <c>medium</c>).
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
    public override string Name => "gemini-3.5-flash";

    /// <inheritdoc />
    public override decimal PriceInput => 1.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 9.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.15m;

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

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 3.1 Pro (preview) — Google's most capable Gemini model for complex
/// reasoning and coding. Still a preview: there is no generally available Gemini
/// 3.x Pro, and preview models can change or be shut down at short notice.
/// </summary>
/// <remarks>
/// <para>
/// 1,048,576 input / 65,536 output tokens. No free API tier.
/// </para>
/// <para>
/// Pricing is <b>tiered on prompt size</b>: $2.00 input / $0.20 cached / $12.00
/// output per 1M tokens up to 200K prompt tokens, and $4.00 / $0.40 / $18.00 above
/// that — applied to every token of the request (see <see cref="GetInputPrice"/>).
/// Batch and Flex at half price.
/// </para>
/// <para>
/// Thinking levels <c>low</c> / <c>medium</c> / <c>high</c> (default); thinking
/// cannot be switched off.
/// </para>
/// </remarks>
public class Gemini31Pro : GoogleThinkingBase<Gemini31Pro.ReasonType>
{
    /// <summary>
    /// Thinking levels accepted by <c>gemini-3.1-pro-preview</c> (<c>low</c> / <c>medium</c> / <c>high</c>; default <c>high</c>).
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
    public override string Name => "gemini-3.1-pro-preview";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 12.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.20m;

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

    /// <summary>Prompt size past which Google bills the whole request at the long-context rate.</summary>
    private const long LongContextThreshold = 200_000;

    /// <summary>$2.00 up to 200K prompt tokens, $4.00 above.</summary>
    public override decimal GetInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold ? PriceInput * 2m : PriceInput;

    /// <summary>$12.00 up to 200K prompt tokens, $18.00 above — keyed on the prompt size.</summary>
    public override decimal GetOutputPrice(long inputTokens, long outputTokens)
        => inputTokens > LongContextThreshold ? PriceOutput * 1.5m : PriceOutput;

    /// <summary>$0.20 up to 200K prompt tokens, $0.40 above.</summary>
    public override decimal GetCachedInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold ? PriceCachedInput!.Value * 2m : PriceCachedInput!.Value;
}

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 2.5 Pro - Google's previous-generation Pro model (stable, June 2025).
/// </summary>
/// <remarks>
/// $1.25 input / $0.125 cached / $10.00 output per 1M tokens up to 200K prompt
/// tokens, $2.50 / $0.25 / $15.00 above. Thinking is always on (dynamic budget).
/// Available only to projects that used Gemini 2.5 before 18 September 2026.
/// </remarks>
[Obsolete("Google limited Gemini 2.5 to accounts that already use it on 18 September 2026. Migrate to Gemini31Pro (gemini-3.1-pro-preview) or Gemini38Flash.")]
public class Gemini25Pro : GoogleBase
{
    /// <inheritdoc />
    public override string Name => "gemini-2.5-pro";

    /// <inheritdoc />
    public override decimal PriceInput => 1.25m;

    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.125m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_048_576;

    /// <inheritdoc />
    public override int MaxOutputTokens => 65_536;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image | ChannelType.Audio;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.CodeInterpreter;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;

    /// <summary>Prompt size past which Google bills the whole request at the long-context rate.</summary>
    private const long LongContextThreshold = 200_000;

    /// <summary>$1.25 up to 200K prompt tokens, $2.50 above.</summary>
    public override decimal GetInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold ? PriceInput * 2m : PriceInput;

    /// <summary>$10.00 up to 200K prompt tokens, $15.00 above.</summary>
    public override decimal GetOutputPrice(long inputTokens, long outputTokens)
        => inputTokens > LongContextThreshold ? PriceOutput * 1.5m : PriceOutput;

    /// <summary>$0.125 up to 200K prompt tokens, $0.25 above.</summary>
    public override decimal GetCachedInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold ? PriceCachedInput!.Value * 2m : PriceCachedInput!.Value;
}

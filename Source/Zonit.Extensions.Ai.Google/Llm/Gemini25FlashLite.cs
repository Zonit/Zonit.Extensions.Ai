namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 2.5 Flash Lite - Fast and cost-effective version of 2.5 Flash (stable, July 2025).
/// </summary>
/// <remarks>
/// $0.10 input / $0.01 cached / $0.40 output per 1M tokens (audio input $0.30).
/// Available only to projects that used Gemini 2.5 before 18 September 2026.
/// </remarks>
[Obsolete("Google limited Gemini 2.5 to accounts that already use it on 18 September 2026. Migrate to Gemini35FlashLite (gemini-3.5-flash-lite) or Gemini31FlashLite.")]
public class Gemini25FlashLite : GoogleBase
{
    /// <inheritdoc />
    public override string Name => "gemini-2.5-flash-lite";

    /// <inheritdoc />
    public override decimal PriceInput => 0.10m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.40m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.01m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_048_576;

    /// <inheritdoc />
    public override int MaxOutputTokens => 65_536;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image | ChannelType.Audio;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

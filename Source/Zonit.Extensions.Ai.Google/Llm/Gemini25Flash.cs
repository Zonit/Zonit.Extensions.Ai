namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 2.5 Flash - Fast Google model with thinking capabilities (stable, June 2025).
/// </summary>
/// <remarks>
/// $0.30 input / $0.03 cached / $2.50 output per 1M tokens (audio input $1.00).
/// Available only to projects that used Gemini 2.5 before 18 September 2026.
/// </remarks>
[Obsolete("Google limited Gemini 2.5 to accounts that already use it on 18 September 2026. Migrate to Gemini38Flash (gemini-3.8-flash).")]
public class Gemini25Flash : GoogleBase
{
    /// <inheritdoc />
    public override string Name => "gemini-2.5-flash";

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
}

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 2.0 Flash Lite - Cost-effective version of Flash.
/// </summary>
public class Gemini20FlashLite : GoogleBase
{
    /// <inheritdoc />
    public override string Name => "gemini-2.0-flash-lite";

    /// <inheritdoc />
    public override decimal PriceInput => 0.075m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.30m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_048_576;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8_192;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

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

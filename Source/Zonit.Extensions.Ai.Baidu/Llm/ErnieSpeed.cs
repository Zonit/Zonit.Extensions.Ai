namespace Zonit.Extensions.Ai.Baidu;

/// <summary>
/// ERNIE Speed - Fast and economical model.
/// Best for high-volume simple tasks.
/// </summary>
public class ErnieSpeed : BaiduBase
{
    /// <inheritdoc />
    public override string Name => "ernie-speed-8k";

    /// <inheritdoc />
    public override decimal PriceInput => 0.40m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.80m;

    /// <inheritdoc />
    public override int MaxInputTokens => 8_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 4_096;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

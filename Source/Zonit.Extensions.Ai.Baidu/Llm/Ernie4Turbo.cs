namespace Zonit.Extensions.Ai.Baidu;

/// <summary>
/// ERNIE 4.0 Turbo - Fast version of ERNIE 4.0.
/// Optimized for speed while maintaining quality.
/// </summary>
public class Ernie4Turbo : BaiduBase
{
    /// <inheritdoc />
    public override string Name => "ernie-4.0-turbo-8k";

    /// <inheritdoc />
    public override decimal PriceInput => 4.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 8.00m;

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
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

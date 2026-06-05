namespace Zonit.Extensions.Ai.Baidu;

/// <summary>
/// ERNIE 3.5 - Balanced performance model.
/// Good quality with reasonable cost.
/// </summary>
public class Ernie3_5 : BaiduBase
{
    /// <inheritdoc />
    public override string Name => "ernie-3.5-8k";

    /// <inheritdoc />
    public override decimal PriceInput => 1.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.20m;

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

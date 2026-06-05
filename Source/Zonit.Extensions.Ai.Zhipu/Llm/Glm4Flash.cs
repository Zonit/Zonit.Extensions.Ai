namespace Zonit.Extensions.Ai.Zhipu;

/// <summary>
/// GLM-4 Flash - Fastest and most economical model.
/// Best for high-volume simple tasks.
/// </summary>
public class Glm4Flash : ZhipuBase
{
    /// <inheritdoc />
    public override string Name => "glm-4-flash";

    /// <inheritdoc />
    public override decimal PriceInput => 0.07m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.07m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

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

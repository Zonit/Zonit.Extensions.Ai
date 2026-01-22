namespace Zonit.Extensions.Ai.Zhipu;

/// <summary>
/// GLM-4 Long - Extended context model.
/// Up to 1M tokens for very long documents.
/// </summary>
public class Glm4Long : ZhipuBase
{
    /// <inheritdoc />
    public override string Name => "glm-4-long";

    /// <inheritdoc />
    public override decimal PriceInput => 0.70m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.70m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_000_000;

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

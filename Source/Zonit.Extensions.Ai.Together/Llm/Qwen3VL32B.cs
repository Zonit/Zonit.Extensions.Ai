namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// Qwen 3 VL 32B on Together AI.
/// Vision-language model with multimodal understanding.
/// </summary>
public class Qwen3VL32B : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "Qwen/Qwen3-VL-32B";

    /// <inheritdoc />
    public override decimal PriceInput => 0.18m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.18m;

    /// <inheritdoc />
    public override int MaxInputTokens => 131_072;

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
        FeaturesType.Vision;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

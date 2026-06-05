namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Cohere Command A Vision - Multimodal flagship with image understanding.
/// 128k context for vision tasks.
/// </summary>
public class CommandAVision : CohereBase
{
    /// <inheritdoc />
    public override string Name => "command-a-03-2025";

    /// <inheritdoc />
    public override decimal PriceInput => 2.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 16_384;

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
        FeaturesType.Vision;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Llama Guard 4 12B on Groq - Safety classifier for content moderation.
/// </summary>
public class LlamaGuard4_12B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "meta-llama/llama-guard-4-12b";

    /// <inheritdoc />
    public override decimal PriceInput => 0.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.20m;

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

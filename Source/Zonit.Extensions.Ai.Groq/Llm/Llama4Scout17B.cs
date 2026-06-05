namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Llama 4 Scout 17B - Latest Llama 4 model on Groq.
/// Multimodal with vision support, efficient MoE architecture.
/// </summary>
public class Llama4Scout17B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "meta-llama/llama-4-scout-17b-16e-instruct";

    /// <inheritdoc />
    public override decimal PriceInput => 0.11m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.34m;

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
        FeaturesType.FunctionCalling |
        FeaturesType.Vision;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

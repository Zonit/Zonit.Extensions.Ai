namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Llama 4 Maverick 17B - Advanced Llama 4 model on Groq.
/// Larger MoE with 128 experts, excellent for complex tasks.
/// </summary>
public class Llama4Maverick17B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "meta-llama/llama-4-maverick-17b-128e-instruct";

    /// <inheritdoc />
    public override decimal PriceInput => 0.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.60m;

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

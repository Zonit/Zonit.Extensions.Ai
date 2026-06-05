namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Gemma 2 9B - Google's Gemma model on Groq.
/// Compact yet capable model for general tasks.
/// </summary>
public class Gemma2_9B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "gemma2-9b-it";

    /// <inheritdoc />
    public override decimal PriceInput => 0.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.20m;

    /// <inheritdoc />
    public override int MaxInputTokens => 8_192;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8_192;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Mixtral 8x7B - Mixture of Experts model on Groq.
/// Excellent for diverse tasks with high efficiency.
/// </summary>
public class Mixtral8x7B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "mixtral-8x7b-32768";

    /// <inheritdoc />
    public override decimal PriceInput => 0.24m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.24m;

    /// <inheritdoc />
    public override int MaxInputTokens => 32_768;

    /// <inheritdoc />
    public override int MaxOutputTokens => 32_768;

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

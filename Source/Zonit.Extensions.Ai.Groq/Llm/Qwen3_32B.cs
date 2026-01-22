namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Qwen 3 32B on Groq - Latest Qwen model with reasoning.
/// </summary>
public class Qwen3_32B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "qwen-qwq-32b";

    /// <inheritdoc />
    public override decimal PriceInput => 0.29m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.59m;

    /// <inheritdoc />
    public override int MaxInputTokens => 131_072;

    /// <inheritdoc />
    public override int MaxOutputTokens => 131_072;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.Reasoning;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

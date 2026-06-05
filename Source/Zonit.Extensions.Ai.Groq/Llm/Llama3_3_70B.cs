namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Llama 3.3 70B - Latest Meta Llama model on Groq.
/// Best for complex reasoning and coding tasks.
/// </summary>
public class Llama3_3_70B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "llama-3.3-70b-versatile";

    /// <inheritdoc />
    public override decimal PriceInput => 0.59m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.79m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

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
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

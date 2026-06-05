namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// Llama 3.1 8B - Fast and efficient Llama model.
/// Great for simple tasks with very low latency.
/// </summary>
public class Llama3_1_8B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "llama-3.1-8b-instant";

    /// <inheritdoc />
    public override decimal PriceInput => 0.05m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.08m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

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
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

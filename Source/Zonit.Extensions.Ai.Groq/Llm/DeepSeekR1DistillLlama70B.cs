namespace Zonit.Extensions.Ai.Groq;

/// <summary>
/// DeepSeek R1 Distill Llama 70B - Reasoning model on Groq.
/// Optimized for complex reasoning tasks.
/// </summary>
public class DeepSeekR1DistillLlama70B : GroqBase
{
    /// <inheritdoc />
    public override string Name => "deepseek-r1-distill-llama-70b";

    /// <inheritdoc />
    public override decimal PriceInput => 0.75m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.99m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 16_384;

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

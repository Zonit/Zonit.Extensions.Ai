namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// DeepSeek R1 on Together AI.
/// Reasoning model with chain-of-thought capabilities.
/// </summary>
public class DeepSeekR1 : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "deepseek-ai/DeepSeek-R1";

    /// <inheritdoc />
    public override decimal PriceInput => 3.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 7.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 64_000;

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
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

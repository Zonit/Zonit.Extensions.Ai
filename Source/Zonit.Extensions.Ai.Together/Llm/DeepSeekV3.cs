namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// DeepSeek V3 on Together AI.
/// Excellent cost-to-performance model.
/// </summary>
public class DeepSeekV3 : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "deepseek-ai/DeepSeek-V3";

    /// <inheritdoc />
    public override decimal PriceInput => 0.90m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.90m;

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
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

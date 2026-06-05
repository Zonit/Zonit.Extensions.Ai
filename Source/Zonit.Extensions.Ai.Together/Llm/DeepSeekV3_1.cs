namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// DeepSeek V3.1 on Together AI.
/// Latest DeepSeek generation with improved performance.
/// </summary>
public class DeepSeekV3_1 : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "deepseek-ai/DeepSeek-V3.1";

    /// <inheritdoc />
    public override decimal PriceInput => 0.49m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.89m;

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
        FeaturesType.FunctionCalling;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

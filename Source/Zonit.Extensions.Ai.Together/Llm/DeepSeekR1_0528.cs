namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// DeepSeek R1 0528 on Together AI.
/// Latest reasoning model with extended thinking.
/// </summary>
public class DeepSeekR1_0528 : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "deepseek-ai/DeepSeek-R1-0528";

    /// <inheritdoc />
    public override decimal PriceInput => 1.65m;

    /// <inheritdoc />
    public override decimal PriceOutput => 7.20m;

    /// <inheritdoc />
    public override int MaxInputTokens => 64_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 64_000;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.Reasoning;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

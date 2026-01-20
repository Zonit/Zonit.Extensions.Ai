namespace Zonit.Extensions.Ai.DeepSeek;

/// <summary>
/// DeepSeek R1 - Reasoning model with extended thinking.
/// Exceptional performance on complex reasoning tasks.
/// </summary>
public class DeepSeekR1 : DeepSeekReasoningBase
{
    /// <inheritdoc />
    public override string Name => "deepseek-reasoner";

    /// <inheritdoc />
    public override decimal PriceInput => 0.28m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.42m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.028m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

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

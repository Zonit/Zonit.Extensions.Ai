namespace Zonit.Extensions.Ai.DeepSeek;

/// <summary>
/// DeepSeek V3 - Latest general-purpose model.
/// Excellent cost-to-performance ratio.
/// </summary>
public class DeepSeekV3 : DeepSeekBase
{
    /// <inheritdoc />
    public override string Name => "deepseek-chat";

    /// <inheritdoc />
    public override decimal PriceInput => 0.28m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.42m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.028m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8_000;

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

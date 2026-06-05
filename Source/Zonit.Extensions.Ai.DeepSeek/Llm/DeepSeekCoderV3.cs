namespace Zonit.Extensions.Ai.DeepSeek;

/// <summary>
/// DeepSeek Coder V3 - Optimized for code generation.
/// </summary>
public class DeepSeekCoderV3 : DeepSeekBase
{
    /// <inheritdoc />
    public override string Name => "deepseek-coder";

    /// <inheritdoc />
    public override decimal PriceInput => 0.27m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.10m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.07m;

    /// <inheritdoc />
    public override int MaxInputTokens => 64_000;

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

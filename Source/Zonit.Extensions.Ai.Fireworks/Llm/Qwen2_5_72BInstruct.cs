namespace Zonit.Extensions.Ai.Fireworks;

/// <summary>
/// Qwen 2.5 72B Instruct on Fireworks.
/// Alibaba's flagship model with excellent capabilities.
/// </summary>
public class Qwen2_5_72BInstruct : FireworksBase
{
    /// <inheritdoc />
    public override string Name => "accounts/fireworks/models/qwen2p5-72b-instruct";

    /// <inheritdoc />
    public override decimal PriceInput => 0.90m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.90m;

    /// <inheritdoc />
    public override int MaxInputTokens => 32_768;

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
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

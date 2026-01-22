namespace Zonit.Extensions.Ai.Alibaba;

/// <summary>
/// Qwen Plus - High-performance balanced model.
/// Great balance of quality and cost.
/// </summary>
public class QwenPlus : AlibabaBase
{
    /// <inheritdoc />
    public override string Name => "qwen-plus";

    /// <inheritdoc />
    public override decimal PriceInput => 0.80m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.00m;

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

namespace Zonit.Extensions.Ai.Alibaba;

/// <summary>
/// Qwen Long - Extended context model.
/// Up to 10M tokens context for long documents.
/// </summary>
public class QwenLong : AlibabaBase
{
    /// <inheritdoc />
    public override string Name => "qwen-long";

    /// <inheritdoc />
    public override decimal PriceInput => 0.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 10_000_000;

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
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

namespace Zonit.Extensions.Ai.Alibaba;

/// <summary>
/// Qwen Max - Alibaba's flagship model.
/// Best for complex reasoning and multilingual tasks.
/// </summary>
public class QwenMax : AlibabaBase
{
    /// <inheritdoc />
    public override string Name => "qwen-max";

    /// <inheritdoc />
    public override decimal PriceInput => 2.40m;

    /// <inheritdoc />
    public override decimal PriceOutput => 9.60m;

    /// <inheritdoc />
    public override int MaxInputTokens => 32_000;

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

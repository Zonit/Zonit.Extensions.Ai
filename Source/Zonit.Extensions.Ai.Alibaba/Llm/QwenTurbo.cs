namespace Zonit.Extensions.Ai.Alibaba;

/// <summary>
/// Qwen Turbo - Fast and cost-effective model.
/// Best for high-volume simple tasks.
/// </summary>
public class QwenTurbo : AlibabaBase
{
    /// <inheritdoc />
    public override string Name => "qwen-turbo";

    /// <inheritdoc />
    public override decimal PriceInput => 0.30m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.60m;

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

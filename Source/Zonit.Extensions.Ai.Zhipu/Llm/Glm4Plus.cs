namespace Zonit.Extensions.Ai.Zhipu;

/// <summary>
/// GLM-4 Plus - Zhipu's flagship model.
/// Best for complex reasoning and understanding.
/// </summary>
public class Glm4Plus : ZhipuBase
{
    /// <inheritdoc />
    public override string Name => "glm-4-plus";

    /// <inheritdoc />
    public override decimal PriceInput => 7.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 7.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 4_096;

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

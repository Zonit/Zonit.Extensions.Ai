namespace Zonit.Extensions.Ai.Zhipu;

/// <summary>
/// GLM-4 Air - Fast and balanced model.
/// Good performance with lower cost.
/// </summary>
public class Glm4Air : ZhipuBase
{
    /// <inheritdoc />
    public override string Name => "glm-4-air";

    /// <inheritdoc />
    public override decimal PriceInput => 0.70m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.70m;

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

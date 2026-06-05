namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// Qwen 2.5 72B Instruct on Together AI.
/// Alibaba's flagship model with excellent multilingual support.
/// </summary>
public class Qwen2_5_72BInstruct : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "Qwen/Qwen2.5-72B-Instruct-Turbo";

    /// <inheritdoc />
    public override decimal PriceInput => 1.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.20m;

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

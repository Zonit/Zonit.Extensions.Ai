namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// Llama 4 Scout 17B on Together AI.
/// Latest multimodal Llama 4 with MoE architecture.
/// </summary>
public class Llama4Scout17B : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "meta-llama/Llama-4-Scout-17B-16E-Instruct";

    /// <inheritdoc />
    public override decimal PriceInput => 0.18m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.59m;

    /// <inheritdoc />
    public override int MaxInputTokens => 512_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 128_000;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.Vision;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

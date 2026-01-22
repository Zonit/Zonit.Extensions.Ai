namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// Llama 4 Maverick 17B on Together AI.
/// Advanced multimodal with 128 experts.
/// </summary>
public class Llama4Maverick17B : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "meta-llama/Llama-4-Maverick-17B-128E-Instruct";

    /// <inheritdoc />
    public override decimal PriceInput => 0.27m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.85m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_048_576;

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

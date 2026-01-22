namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// Qwen 2.5 Coder 32B Instruct on Together AI.
/// Specialized coding model with excellent performance.
/// </summary>
public class Qwen2_5_Coder32BInstruct : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "Qwen/Qwen2.5-Coder-32B-Instruct";

    /// <inheritdoc />
    public override decimal PriceInput => 0.80m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.80m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

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
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

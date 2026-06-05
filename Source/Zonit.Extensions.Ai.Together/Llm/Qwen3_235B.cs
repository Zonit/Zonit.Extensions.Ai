namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// Qwen 3 235B A22B on Together AI.
/// Largest Qwen 3 MoE model with advanced reasoning.
/// </summary>
public class Qwen3_235B : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "Qwen/Qwen3-235B-A22B";

    /// <inheritdoc />
    public override decimal PriceInput => 0.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.50m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 32_768;

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
        FeaturesType.Reasoning;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

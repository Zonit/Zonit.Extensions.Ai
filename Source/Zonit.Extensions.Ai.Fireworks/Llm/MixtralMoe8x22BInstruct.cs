namespace Zonit.Extensions.Ai.Fireworks;

/// <summary>
/// Mixtral MoE 8x22B Instruct on Fireworks.
/// Large mixture of experts model.
/// </summary>
public class MixtralMoe8x22BInstruct : FireworksBase
{
    /// <inheritdoc />
    public override string Name => "accounts/fireworks/models/mixtral-8x22b-instruct";

    /// <inheritdoc />
    public override decimal PriceInput => 1.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.20m;

    /// <inheritdoc />
    public override int MaxInputTokens => 65_536;

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
        FeaturesType.FunctionCalling;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

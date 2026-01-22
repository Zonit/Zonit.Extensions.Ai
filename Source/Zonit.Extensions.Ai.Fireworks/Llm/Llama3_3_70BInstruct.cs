namespace Zonit.Extensions.Ai.Fireworks;

/// <summary>
/// Llama 3.3 70B Instruct on Fireworks.
/// High performance model with fast inference.
/// </summary>
public class Llama3_3_70BInstruct : FireworksBase
{
    /// <inheritdoc />
    public override string Name => "accounts/fireworks/models/llama-v3p3-70b-instruct";

    /// <inheritdoc />
    public override decimal PriceInput => 0.90m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.90m;

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
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

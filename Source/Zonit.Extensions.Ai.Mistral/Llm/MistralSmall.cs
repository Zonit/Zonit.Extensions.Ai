namespace Zonit.Extensions.Ai.Mistral;

/// <summary>
/// Mistral Small - Fast and cost-effective.
/// Best for simple tasks with low latency requirements.
/// </summary>
public class MistralSmall : MistralBase
{
    /// <inheritdoc />
    public override string Name => "mistral-small-latest";

    /// <inheritdoc />
    public override decimal PriceInput => 0.10m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.30m;

    /// <inheritdoc />
    public override int MaxInputTokens => 32_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8_192;

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

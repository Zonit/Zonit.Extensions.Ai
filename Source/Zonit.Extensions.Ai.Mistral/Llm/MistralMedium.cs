namespace Zonit.Extensions.Ai.Mistral;

/// <summary>
/// Mistral Medium - Balanced performance and cost.
/// Ideal for intermediate complexity tasks.
/// </summary>
public class MistralMedium : MistralBase
{
    /// <inheritdoc />
    public override string Name => "mistral-medium-latest";

    /// <inheritdoc />
    public override decimal PriceInput => 0.40m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8_192;

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
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Aya Expanse 32B - Multilingual model for 23+ languages.
/// Optimized for diverse global content.
/// </summary>
public class AyaExpanse32B : CohereBase
{
    /// <inheritdoc />
    public override string Name => "aya-expanse-32b";

    /// <inheritdoc />
    public override decimal PriceInput => 0.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.50m;

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
        FeaturesType.Streaming;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

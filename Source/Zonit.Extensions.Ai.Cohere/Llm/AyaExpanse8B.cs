namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Aya Expanse 8B - Lightweight multilingual model.
/// Efficient for multi-language tasks with low cost.
/// </summary>
public class AyaExpanse8B : CohereBase
{
    /// <inheritdoc />
    public override string Name => "aya-expanse-8b";

    /// <inheritdoc />
    public override decimal PriceInput => 0.05m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.15m;

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

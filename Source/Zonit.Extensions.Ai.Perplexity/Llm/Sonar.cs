namespace Zonit.Extensions.Ai.Perplexity;

/// <summary>
/// Sonar - Standard Perplexity model.
/// Fast search-augmented responses.
/// </summary>
public class Sonar : PerplexityBase
{
    /// <inheritdoc />
    public override string Name => "sonar";

    /// <inheritdoc />
    public override decimal PriceInput => 1.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8_000;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

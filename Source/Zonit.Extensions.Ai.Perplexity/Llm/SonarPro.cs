namespace Zonit.Extensions.Ai.Perplexity;

/// <summary>
/// Sonar Pro - Most capable Perplexity model.
/// Advanced reasoning with real-time web search.
/// </summary>
public class SonarPro : PerplexityBase
{
    /// <inheritdoc />
    public override string Name => "sonar-pro";

    /// <inheritdoc />
    public override decimal PriceInput => 3.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 15.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 200_000;

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

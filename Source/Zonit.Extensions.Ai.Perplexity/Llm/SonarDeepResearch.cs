namespace Zonit.Extensions.Ai.Perplexity;

/// <summary>
/// Sonar Deep Research - Extended research model.
/// In-depth research with multiple search queries.
/// </summary>
public class SonarDeepResearch : PerplexityBase
{
    /// <inheritdoc />
    public override string Name => "sonar-deep-research";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 8.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 16_000;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.Reasoning;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

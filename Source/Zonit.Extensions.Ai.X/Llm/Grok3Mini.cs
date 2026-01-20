namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 3 Mini - Fast and cost-effective Grok model with reasoning.
/// </summary>
public class Grok3Mini : XReasoningBase
{
    /// <inheritdoc />
    public override string Name => "grok-3-mini";

    /// <inheritdoc />
    public override decimal PriceInput => 0.30m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.075m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.50m;

    /// <inheritdoc />
    public override int MaxInputTokens => 131_072;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8_192;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
    
    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;
}

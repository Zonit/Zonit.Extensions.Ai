namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.20 Multi-Agent - Multi-agent variant of Grok 4.20 with parallel agent
/// coordination. The <see cref="XReasoningBase.Reason"/> property selects the
/// number of collaborating agents (low / medium / high / xhigh) — it does not
/// control thinking depth. This is the only Grok model that accepts the
/// <c>reasoning.effort</c> parameter; sending it to grok-4.3 / grok-4.20
/// reasoning / grok-4-1-fast returns an API error.
/// </summary>
/// <remarks>
/// Pricing: $1.25/$2.50 per 1M tokens, $0.3125 cached input.
/// Higher context pricing applies above 200K tokens.
/// </remarks>
public class Grok420MultiAgent : XReasoningBase
{
    /// <inheritdoc />
    public override string Name => "grok-4.20-multi-agent-0309";

    /// <inheritdoc />
    public override decimal PriceInput => 1.25m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.3125m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.50m;

    /// <summary>
    /// Multi-agent is the sole Grok model that accepts <c>reasoning.effort</c>
    /// on the wire — every other reasoning model rejects it.
    /// </summary>
    internal override bool EmitsReasoningEffort => true;

    /// <inheritdoc />
    public override int MaxInputTokens => 2_000_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 131_072;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools =>
        ToolsType.WebSearch |
        ToolsType.CodeExecution;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.Reasoning;

    /// <inheritdoc />
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 200_000 ? PriceInput * 2 : PriceInput;
    }

    /// <inheritdoc />
    public override decimal GetOutputPrice(long tokenCount)
    {
        return tokenCount > 200_000 ? PriceOutput * 2 : PriceOutput;
    }
}

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.1 Fast Non-Reasoning - High-speed model without reasoning overhead.
/// Best for quick responses where deep reasoning is not required.
/// </summary>
public class Grok41FastNonReasoning : XChatBase
{
    /// <inheritdoc />
    public override string Name => "grok-4-1-fast-non-reasoning";

    /// <inheritdoc />
    public override decimal PriceInput => 0.20m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.05m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.50m;

    /// <inheritdoc />
    public override int MaxInputTokens => 2_000_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 131_072;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

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

    /// <summary>
    /// Extended context pricing for input.
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        if (tokenCount <= 128_000)
            return PriceInput;
        return PriceInput * 2;
    }

    /// <summary>
    /// Extended context pricing for output.
    /// </summary>
    public override decimal GetOutputPrice(long tokenCount)
    {
        if (tokenCount <= 128_000)
            return PriceOutput;
        return PriceOutput * 2;
    }
}

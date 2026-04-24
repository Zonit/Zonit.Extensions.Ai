namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.20 Non-Reasoning - xAI's flagship model without reasoning overhead.
/// Optimized for high-speed responses where deep reasoning is not required.
/// </summary>
/// <remarks>
/// For more information, see: https://docs.x.ai/docs/models/grok-4.20-non-reasoning
/// Pricing: $2.00/$6.00 per 1M tokens, $0.20 cached input.
/// Higher context pricing applies above 200K tokens.
/// </remarks>
public class Grok420NonReasoning : XChatBase
{
    /// <inheritdoc />
    public override string Name => "grok-4.20-beta-0309-non-reasoning";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 6.00m;

    /// <inheritdoc />
    /// <remarks>2M context window.</remarks>
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
        FeaturesType.StructuredOutputs;

    /// <summary>
    /// Higher context pricing: requests exceeding the 200K context window
    /// are charged at 2x the base rate.
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 200_000 ? PriceInput * 2 : PriceInput;
    }

    /// <summary>
    /// Higher context pricing for output: requests exceeding the 200K context window
    /// are charged at 2x the base rate.
    /// </summary>
    public override decimal GetOutputPrice(long tokenCount)
    {
        return tokenCount > 200_000 ? PriceOutput * 2 : PriceOutput;
    }
}

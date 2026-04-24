namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.20 Beta - xAI's most intelligent and fastest model. General flagship variant
/// balancing performance and speed for everyday development tasks.
/// </summary>
/// <remarks>
/// Pricing: $2.00/$6.00 per 1M tokens, 2M context window.
/// Higher context pricing applies above 200K tokens.
/// </remarks>
public class Grok420 : XChatBase
{
    /// <inheritdoc />
    public override string Name => "grok-4.20-beta";

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

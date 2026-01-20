namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.1 Fast Reasoning - Fast model with full reasoning capabilities.
/// Reasoning is always enabled (cannot be disabled).
/// </summary>
public class Grok41FastReasoning : XReasoningBase
{
    /// <inheritdoc />
    public override string Name => "grok-4.1-fast-reasoning";

    /// <inheritdoc />
    public override decimal PriceInput => 0.50m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.125m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.00m;

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
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.Reasoning;

    /// <summary>
    /// Extended context pricing for input.
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        if (tokenCount <= 128_000)
            return PriceInput;
        if (tokenCount <= 512_000)
            return PriceInput * 2;
        return PriceInput * 4;
    }

    /// <summary>
    /// Extended context pricing for output.
    /// </summary>
    public override decimal GetOutputPrice(long tokenCount)
    {
        if (tokenCount <= 128_000)
            return PriceOutput;
        if (tokenCount <= 512_000)
            return PriceOutput * 2;
        return PriceOutput * 4;
    }
}

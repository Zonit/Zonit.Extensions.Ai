namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.1 Fast - Fast, multimodal, function calling, structured outputs, reasoning.
/// Has 2M context window with extended context pricing.
/// Lower cost and higher speed than Grok 4.
/// </summary>
public class Grok41Fast : XReasoningBase
{
    /// <inheritdoc />
    public override string Name => "grok-4-1-fast";

    /// <inheritdoc />
    /// <remarks>Base price per 1M input tokens.</remarks>
    public override decimal PriceInput => 0.50m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.125m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.00m;

    /// <inheritdoc />
    /// <remarks>2M context window!</remarks>
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
    /// Grok 4.1 Fast supports reasoning_effort parameter (low, high).
    /// Default: null (model decides).
    /// </summary>
    public override ReasonType? Reason { get; init; }

    /// <summary>
    /// Extended context pricing:
    /// - Base: $0.50/1M (0-128k)
    /// - 128k-512k: 2x price ($1.00/1M)
    /// - 512k-2M: 4x price ($2.00/1M)
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
    /// Extended context pricing for output:
    /// - Base: $2.00/1M (0-128k)
    /// - 128k-512k: 2x price ($4.00/1M)
    /// - 512k-2M: 4x price ($8.00/1M)
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

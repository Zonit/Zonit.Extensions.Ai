namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4 - X.ai's reasoning-only model.
/// NOTE: Grok 4 does NOT support reasoning_effort parameter (it's always "on").
/// For reasoning with configurable effort, use Grok 3.
/// </summary>
public class Grok4 : XReasoningBase
{
    /// <inheritdoc />
    public override string Name => "grok-4-0709";

    /// <inheritdoc />
    public override decimal PriceInput => 3.00m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.75m;

    /// <inheritdoc />
    public override decimal PriceOutput => 15.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 256_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 65_536;

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
        FeaturesType.StructuredOutputs |
        FeaturesType.Reasoning;

    /// <summary>
    /// Grok 4 does NOT support reasoning_effort parameter.
    /// Returns null to prevent sending the parameter.
    /// </summary>
    public override ReasoningEffort? Reason => null;

    /// <summary>
    /// Grok4 has double price for contexts above 128k tokens.
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 128_000 ? PriceInput * 2 : PriceInput;
    }

    /// <summary>
    /// Output also has double price above 128k tokens.
    /// </summary>
    public override decimal GetOutputPrice(long tokenCount)
    {
        return tokenCount > 128_000 ? PriceOutput * 2 : PriceOutput;
    }
}

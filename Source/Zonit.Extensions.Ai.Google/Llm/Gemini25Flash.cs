namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 2.5 Flash - Fast Google model with thinking capabilities.
/// </summary>
public class Gemini25Flash : GoogleBase
{
    /// <inheritdoc />
    public override string Name => "gemini-2.5-flash-preview-05-20";

    /// <inheritdoc />
    public override decimal PriceInput => 0.15m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.60m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_048_576;

    /// <inheritdoc />
    public override int MaxOutputTokens => 65_536;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image | ChannelType.Audio;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.WebSearch | ToolsType.CodeInterpreter;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

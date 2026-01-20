namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 2.0 Flash - Cost-effective Google model.
/// </summary>
public class Gemini20Flash : GoogleBase
{
    /// <inheritdoc />
    public override string Name => "gemini-2.0-flash";

    /// <inheritdoc />
    public override decimal PriceInput => 0.10m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 0.40m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_048_576;
    
    /// <inheritdoc />
    public override int MaxOutputTokens => 8_192;

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

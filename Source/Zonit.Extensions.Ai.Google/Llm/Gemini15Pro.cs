namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 1.5 Pro - Previous generation capable model.
/// </summary>
public class Gemini15Pro : GoogleBase
{
    /// <inheritdoc />
    public override string Name => "gemini-1.5-pro";

    /// <inheritdoc />
    public override decimal PriceInput => 1.25m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 5.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 2_097_152;
    
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

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 2.5 Pro - Most capable Google model.
/// </summary>
public class Gemini25Pro : GoogleBase
{
    /// <inheritdoc />
    public override string Name => "gemini-2.5-pro-preview-06-05";

    /// <inheritdoc />
    public override decimal PriceInput => 1.25m;
    
    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;

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

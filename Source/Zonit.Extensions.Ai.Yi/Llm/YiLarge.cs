namespace Zonit.Extensions.Ai.Yi;

/// <summary>
/// Yi Large - Flagship 01.AI model with advanced reasoning.
/// </summary>
/// <remarks>
/// The most capable Yi model featuring:
/// <list type="bullet">
///   <item>Complex reasoning and analysis</item>
///   <item>Strong code generation</item>
///   <item>Excellent bilingual performance</item>
/// </list>
/// </remarks>
public sealed class YiLarge : YiBase
{
    /// <inheritdoc />
    public override string Name => "yi-large";

    /// <inheritdoc />
    public override decimal PriceInput => 3.0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 3.0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 32768;

    /// <inheritdoc />
    public override int MaxOutputTokens => 16384;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

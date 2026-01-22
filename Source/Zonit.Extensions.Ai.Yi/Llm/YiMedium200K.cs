namespace Zonit.Extensions.Ai.Yi;

/// <summary>
/// Yi Medium 200K - Extended context version of Yi Medium.
/// </summary>
/// <remarks>
/// Extended context for:
/// <list type="bullet">
///   <item>Long document processing</item>
///   <item>Extended conversations</item>
///   <item>200K token context window</item>
/// </list>
/// </remarks>
public sealed class YiMedium200K : YiBase
{
    /// <inheritdoc />
    public override string Name => "yi-medium-200k";

    /// <inheritdoc />
    public override decimal PriceInput => 1.2m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.2m;

    /// <inheritdoc />
    public override int MaxInputTokens => 200000;

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

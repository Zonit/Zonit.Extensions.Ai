namespace Zonit.Extensions.Ai.Yi;

/// <summary>
/// Yi Medium - Balanced performance model.
/// </summary>
/// <remarks>
/// Good balance between:
/// <list type="bullet">
///   <item>Performance and cost</item>
///   <item>32K context window</item>
///   <item>General-purpose tasks</item>
/// </list>
/// </remarks>
public sealed class YiMedium : YiBase
{
    /// <inheritdoc />
    public override string Name => "yi-medium";

    /// <inheritdoc />
    public override decimal PriceInput => 0.8m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.8m;

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

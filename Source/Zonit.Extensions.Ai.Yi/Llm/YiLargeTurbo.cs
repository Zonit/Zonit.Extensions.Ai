namespace Zonit.Extensions.Ai.Yi;

/// <summary>
/// Yi Large Turbo - Speed-optimized version of Yi Large.
/// </summary>
/// <remarks>
/// Optimized for:
/// <list type="bullet">
///   <item>Faster inference times</item>
///   <item>Lower latency for production use</item>
///   <item>Balanced quality vs speed tradeoff</item>
/// </list>
/// </remarks>
public sealed class YiLargeTurbo : YiBase
{
    /// <inheritdoc />
    public override string Name => "yi-large-turbo";

    /// <inheritdoc />
    public override decimal PriceInput => 1.0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 16384;

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

namespace Zonit.Extensions.Ai.Moonshot;

/// <summary>
/// Moonshot V1 128K - Maximum context window up to 128K tokens.
/// </summary>
/// <remarks>
/// Best for:
/// <list type="bullet">
///   <item>Analyzing very long documents</item>
///   <item>Extended multi-turn conversations</item>
///   <item>Complex reasoning tasks requiring extensive context</item>
/// </list>
/// </remarks>
public sealed class MoonshotV1_128K : MoonshotBase
{
    /// <inheritdoc />
    public override string Name => "moonshot-v1-128k";

    /// <inheritdoc />
    public override decimal PriceInput => 6.0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 6.0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 131072;

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

namespace Zonit.Extensions.Ai.Moonshot;

/// <summary>
/// Moonshot V1 32K - Extended context window up to 32K tokens.
/// </summary>
/// <remarks>
/// Suitable for document analysis and longer conversations.
/// </remarks>
public sealed class MoonshotV1_32K : MoonshotBase
{
    /// <inheritdoc />
    public override string Name => "moonshot-v1-32k";

    /// <inheritdoc />
    public override decimal PriceInput => 2.0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 32768;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8192;

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

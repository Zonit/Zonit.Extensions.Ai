namespace Zonit.Extensions.Ai.Moonshot;

/// <summary>
/// Moonshot V1 8K - Fast responses with 8K context window.
/// </summary>
/// <remarks>
/// Optimized for quick responses with moderate context requirements.
/// </remarks>
public sealed class MoonshotV1_8K : MoonshotBase
{
    /// <inheritdoc />
    public override string Name => "moonshot-v1-8k";

    /// <inheritdoc />
    public override decimal PriceInput => 1.0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 1.0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 8192;

    /// <inheritdoc />
    public override int MaxOutputTokens => 4096;

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

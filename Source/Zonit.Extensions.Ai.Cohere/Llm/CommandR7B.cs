namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Cohere Command R 7B - Lightweight model optimized for speed.
/// Best for simple tasks and low-latency applications.
/// </summary>
public class CommandR7B : CohereBase
{
    /// <inheritdoc />
    public override string Name => "command-r7b-12-2024";

    /// <inheritdoc />
    public override decimal PriceInput => 0.0375m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.15m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 4_096;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

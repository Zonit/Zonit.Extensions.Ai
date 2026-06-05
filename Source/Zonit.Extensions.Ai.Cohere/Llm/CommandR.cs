namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Cohere Command R - Balanced model for general tasks.
/// Great for RAG applications with good cost efficiency.
/// </summary>
public class CommandR : CohereBase
{
    /// <inheritdoc />
    public override string Name => "command-r";

    /// <inheritdoc />
    public override decimal PriceInput => 0.15m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.60m;

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
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

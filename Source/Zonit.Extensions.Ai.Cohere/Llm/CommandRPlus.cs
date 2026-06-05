namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Cohere Command R+ - Most capable model for complex tasks.
/// Optimized for RAG and enterprise use cases.
/// </summary>
public class CommandRPlus : CohereBase
{
    /// <inheritdoc />
    public override string Name => "command-r-plus";

    /// <inheritdoc />
    public override decimal PriceInput => 2.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;

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

namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Cohere Command A Reasoning - Extended thinking with 32k output.
/// Optimized for complex reasoning tasks with detailed responses.
/// </summary>
public class CommandAReasoning : CohereBase
{
    /// <inheritdoc />
    public override string Name => "command-a-03-2025";

    /// <inheritdoc />
    public override decimal PriceInput => 2.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 256_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 32_768;

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
        FeaturesType.StructuredOutputs |
        FeaturesType.Reasoning;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

namespace Zonit.Extensions.Ai.Mistral;

/// <summary>
/// Mistral Embed - Embedding model for text vectorization.
/// </summary>
public class MistralEmbed : MistralBase, IEmbeddingLlm
{
    /// <inheritdoc />
    public override string Name => "mistral-embed";

    /// <inheritdoc />
    public override decimal PriceInput => 0.10m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 8_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 1_024;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Embedding;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures => FeaturesType.None;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Embedding;

    /// <summary>
    /// Number of dimensions for the embedding output.
    /// </summary>
    public int Dimensions { get; set; } = 1024;
}

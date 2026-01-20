namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Text Embedding 004 - Google's embedding model.
/// </summary>
public class TextEmbedding004 : GoogleBase, IEmbeddingLlm
{
    /// <inheritdoc />
    public override string Name => "text-embedding-004";

    /// <inheritdoc />
    public override decimal PriceInput => 0.025m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 2_048;

    /// <inheritdoc />
    public override int MaxOutputTokens => 768;

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
    public int Dimensions { get; set; } = 768;
}

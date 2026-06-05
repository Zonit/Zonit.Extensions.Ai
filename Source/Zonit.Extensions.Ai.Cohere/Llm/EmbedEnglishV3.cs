namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Cohere Embed English v3 - High-quality English embeddings.
/// Optimized for semantic search and RAG applications.
/// </summary>
public class EmbedEnglishV3 : CohereEmbeddingBase
{
    /// <inheritdoc />
    public override string Name => "embed-english-v3.0";

    /// <inheritdoc />
    public override decimal PriceInput => 0.10m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 512;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0;

    /// <inheritdoc />
    public override int Dimensions => 1024;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures => FeaturesType.None;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Embedding;
}

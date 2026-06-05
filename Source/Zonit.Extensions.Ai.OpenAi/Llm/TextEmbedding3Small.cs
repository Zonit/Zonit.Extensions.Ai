namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Text Embedding 3 Small - Cost-efficient embedding model.
/// 1536 dimensions by default.
/// </summary>
public class TextEmbedding3Small : OpenAiBase, IEmbeddingLlm
{
    /// <inheritdoc />
    public override string Name => "text-embedding-3-small";

    /// <inheritdoc />
    public override decimal PriceInput => 0.02m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 8_191;

    /// <inheritdoc />
    public override int MaxOutputTokens => 1_536;

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
    /// Can be reduced for smaller storage.
    /// </summary>
    public int Dimensions { get; set; } = 1536;
}

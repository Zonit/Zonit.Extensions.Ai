namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini Embedding 2 - Google's current embedding model (GA 22 April 2026),
/// replacing <c>text-embedding-004</c>.
/// </summary>
/// <remarks>
/// Up to 8,192 input tokens; output dimensionality 128–3072 (default 3072), set
/// through <see cref="Dimensions"/>. $0.20 per 1M text tokens — image ($0.45),
/// audio ($6.50) and video ($12.00) inputs are priced higher but are not sent by
/// this text-only embedding API.
/// </remarks>
public class GeminiEmbedding2 : GoogleBase, IEmbeddingLlm
{
    /// <inheritdoc />
    public override string Name => "gemini-embedding-2";

    /// <inheritdoc />
    public override decimal PriceInput => 0.20m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 8_192;

    /// <inheritdoc />
    public override int MaxOutputTokens => 3_072;

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
    /// Output dimensionality (128–3072), sent as <c>outputDimensionality</c>.
    /// Google recommends 768, 1536 or 3072; smaller vectors trade a little quality
    /// for storage.
    /// </summary>
    public int Dimensions { get; set; } = 3072;
}

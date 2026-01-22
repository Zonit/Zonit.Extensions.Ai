namespace Zonit.Extensions.Ai.Cohere;

/// <summary>
/// Base class for Cohere embedding models.
/// </summary>
public abstract class CohereEmbeddingBase : LlmBase, IEmbeddingLlm
{
    /// <inheritdoc />
    public abstract int Dimensions { get; }
}

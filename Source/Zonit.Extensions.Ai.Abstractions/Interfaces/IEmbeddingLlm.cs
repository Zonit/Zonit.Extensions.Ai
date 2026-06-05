namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that supports embeddings generation.
/// </summary>
public interface IEmbeddingLlm : ILlm
{
    /// <summary>
    /// Embedding vector dimensions.
    /// </summary>
    int Dimensions { get; }
}

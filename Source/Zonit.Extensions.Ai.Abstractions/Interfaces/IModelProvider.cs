using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Interface for specific AI provider implementations (OpenAI, Anthropic, etc.).
/// Each provider package (Zonit.Extensions.Ai.OpenAi, etc.) implements this.
/// </summary>
public interface IModelProvider
{
    /// <summary>
    /// Provider name for logging and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks if this provider supports the given model.
    /// Used by the main AiProvider to route requests.
    /// </summary>
    /// <param name="llm">The model to check.</param>
    /// <returns>True if supported.</returns>
    bool SupportsModel(ILlm llm);

    /// <summary>
    /// Generates a structured response.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    Task<Result<TResponse>> GenerateAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an image.
    /// </summary>
    Task<Result<File>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<File> prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams text response.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    IAsyncEnumerable<string> StreamAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes audio.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    Task<Result<string>> TranscribeAsync(
        IAudioLlm llm,
        File audioFile,
        string? language = null,
        CancellationToken cancellationToken = default);
}

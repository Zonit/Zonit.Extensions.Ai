using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions;

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
    /// Returns an Asset containing the generated image.
    /// </summary>
    Task<Result<Asset>> GenerateImageAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a video.
    /// Returns an Asset containing the generated video.
    /// </summary>
    Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
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
    /// Multi-turn chat completion: <paramref name="prompt"/> supplies the system
    /// instruction (its rendered <c>Text</c>) and <paramref name="chat"/> carries
    /// the User/Assistant/Tool history.
    /// </summary>
    /// <remarks>
    /// Default implementation falls back to the single-shot path by collapsing
    /// <paramref name="chat"/> into a synthetic prompt — providers that handle
    /// chat natively (Anthropic Messages, OpenAI Responses) override this.
    /// </remarks>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    Task<Result<TResponse>> ChatAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        CancellationToken cancellationToken = default)
        => GenerateAsync(llm, ChatFallback.GlueToPrompt(prompt, chat), cancellationToken);

    /// <summary>
    /// Streaming multi-turn chat. Default falls back to single-shot streaming
    /// over a glued prompt — providers with native chat streaming override this.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    IAsyncEnumerable<string> ChatStreamAsync(
        ILlm llm,
        IPrompt prompt,
        IReadOnlyList<ChatMessage> chat,
        CancellationToken cancellationToken = default)
        => StreamAsync(llm, ChatFallback.GlueToPrompt<string>(new ChatFallback.PromptShim(prompt), chat), cancellationToken);

    /// <summary>
    /// Transcribes audio.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    Task<Result<string>> TranscribeAsync(
        IAudioLlm llm,
        Asset audioFile,
        string? language = null,
        CancellationToken cancellationToken = default);
}

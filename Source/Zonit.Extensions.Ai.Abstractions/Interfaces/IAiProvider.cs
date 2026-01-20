using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Main AI provider interface - the primary API for AI operations.
/// Inject this interface to use AI capabilities.
/// </summary>
public interface IAiProvider
{
    #region Text Generation

    [Obsolete("Support legacy, use GenerateAsync<TResponse>(ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default)")]
    Task<Result<TResponse>> GenerateAsync<TResponse>(
        IPrompt<TResponse> prompt,
        ILlm llm,
        CancellationToken cancellationToken = default) 
            => GenerateAsync(llm, prompt, cancellationToken);

    /// <summary>
    /// Generates a structured response from a typed prompt.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    Task<Result<TResponse>> GenerateAsync<TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a text response from a simple string prompt.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    Task<Result<string>> GenerateAsync(
        ILlm llm,
        string prompt,
        CancellationToken cancellationToken = default);

    #endregion

    #region Image Generation

    /// <summary>
    /// Generates an image from a text description.
    /// </summary>
    Task<Result<File>> GenerateAsync(
        IImageLlm llm,
        string description,
        CancellationToken cancellationToken = default);

    #endregion

    #region Embeddings

    /// <summary>
    /// Generates text embeddings (vector representation).
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    Task<Result<float[]>> GenerateAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default);

    #endregion

    #region Audio

    /// <summary>
    /// Transcribes audio to text.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    Task<Result<string>> GenerateAsync(
        IAudioLlm llm,
        File audioFile,
        string? language = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Streaming

    /// <summary>
    /// Streams a text response token by token.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    IAsyncEnumerable<string> StreamAsync(
        ILlm llm,
        string prompt,
        CancellationToken cancellationToken = default);

    #endregion

    #region Cost Calculation

    /// <summary>
    /// Calculates cost for text model based on token counts.
    /// </summary>
    Price CalculateCost(ILlm llm, int inputTokens, int outputTokens);

    /// <summary>
    /// Calculates cost for image generation.
    /// </summary>
    Price CalculateCost(IImageLlm llm, int imageCount = 1);

    /// <summary>
    /// Calculates cost for embedding.
    /// </summary>
    Price CalculateCost(IEmbeddingLlm llm, int inputTokens);

    /// <summary>
    /// Calculates cost for audio transcription.
    /// </summary>
    Price CalculateCost(IAudioLlm llm, int durationSeconds);

    /// <summary>
    /// Estimates cost for a prompt before sending.
    /// </summary>
    Price EstimateCost(ILlm llm, string promptText, int estimatedOutputTokens = 500);

    #endregion
}

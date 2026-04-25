using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions;

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
    /// Returns an Asset containing the generated image.
    /// </summary>
    Task<Result<Asset>> GenerateAsync(
        IImageLlm llm,
        string description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an image from an image prompt.
    /// Returns an Asset containing the generated image.
    /// </summary>
    Task<Result<Asset>> GenerateAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default);

    #endregion

    #region Video Generation

    /// <summary>
    /// Generates a video from a text description.
    /// Returns an Asset containing the generated video.
    /// </summary>
    Task<Result<Asset>> GenerateAsync(
        IVideoLlm llm,
        string description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a video from a video prompt.
    /// Returns an Asset containing the generated video.
    /// </summary>
    Task<Result<Asset>> GenerateAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
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
        Asset audioFile,
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

    #region Agent

    /// <summary>
    /// Runs an agent loop: drives the model, executes tool calls on our side,
    /// feeds results back, until the model produces a final structured answer.
    /// </summary>
    /// <typeparam name="TResponse">Final response type (parsed from structured output).</typeparam>
    /// <param name="llm">Agent-capable LLM.</param>
    /// <param name="prompt">Initial user prompt.</param>
    /// <param name="tools">
    /// Explicit tool list for this invocation. When <c>null</c>, all tools registered
    /// via <c>AddAiTool&lt;T&gt;()</c> are used.
    /// </param>
    /// <param name="mcps">
    /// Explicit MCP server list for this invocation. When <c>null</c>, servers
    /// registered via <c>AddAiMcp(...)</c> are used.
    /// </param>
    /// <param name="options">Per-call overrides (iterations, timeout, cost cap, allow-list, <c>OnToolCall</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    Task<ResultAgent<TResponse>> GenerateAsync<TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Agent-mode overload for plain-text output (no structured response type).
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    Task<ResultAgent<string>> GenerateAsync(
        IAgentLlm llm,
        string prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams an agent run as a sequence of <see cref="AgentEvent"/>s.
    /// </summary>
    /// <remarks>
    /// Event order per iteration:
    /// <list type="number">
    ///   <item><description><see cref="AgentIterationStartedEvent"/></description></item>
    ///   <item><description><see cref="AgentTurnCompletedEvent"/> (after the model responds)</description></item>
    ///   <item><description><see cref="AgentToolCallStartedEvent"/> / <see cref="AgentToolCallCompletedEvent"/> (one pair per tool call, fan-out in parallel)</description></item>
    /// </list>
    /// The stream always terminates with either
    /// <see cref="AgentFinalTextEvent"/> + <see cref="AgentCompletedEvent{TResponse}"/>
    /// (success) or <see cref="AgentFailedEvent"/> (failure).
    /// </remarks>
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    IAsyncEnumerable<AgentEvent> StreamAgentAsync<TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
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
    /// Calculates cost for video generation.
    /// </summary>
    Price CalculateCost(IVideoLlm llm, int durationSeconds, int videoCount = 1);

    /// <summary>
    /// Estimates cost for a prompt before sending.
    /// </summary>
    Price EstimateCost(ILlm llm, string promptText, int estimatedOutputTokens = 500);

    #endregion
}

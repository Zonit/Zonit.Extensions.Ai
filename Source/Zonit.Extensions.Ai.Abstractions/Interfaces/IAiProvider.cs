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
    Task<Result<TResponse>> GenerateAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IPrompt<TResponse> prompt,
        ILlm llm,
        CancellationToken cancellationToken = default)
            => GenerateAsync(llm, prompt, cancellationToken);

    /// <summary>
    /// Generates a structured response from a typed prompt.
    /// </summary>
    Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a text response from a simple string prompt.
    /// </summary>
    Task<Result<string>> GenerateAsync(
        ILlm llm,
        string prompt,
        CancellationToken cancellationToken = default);

    #endregion

    #region Chat

    /// <summary>
    /// Multi-turn chat completion. The <paramref name="prompt"/> supplies the
    /// system instruction (its rendered <c>Text</c> is the system message);
    /// the conversation timeline lives in <paramref name="chat"/> as a sequence
    /// of <see cref="User"/>, <see cref="Assistant"/>, and <see cref="Tool"/>
    /// records.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Semantic difference vs <c>GenerateAsync(llm, prompt)</c>: in chat mode
    /// <c>prompt.Text</c> becomes the <b>system</b> message (not the user
    /// message). In single-shot <c>GenerateAsync</c>, <c>prompt.Text</c> is the
    /// user message and <c>prompt.System</c> the optional system message.
    /// </para>
    /// <para>
    /// Files on <c>prompt.Files</c> are forwarded as session-level attachments;
    /// per-turn files on <see cref="User.Files"/> are forwarded with that turn.
    /// Both can be supplied.
    /// </para>
    /// </remarks>
    Task<Result<TResponse>> ChatAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Plain-text overload. <paramref name="systemPrompt"/> is the system instruction (may be empty).
    /// </summary>
    Task<Result<string>> ChatAsync(
        ILlm llm,
        string systemPrompt,
        IReadOnlyList<ChatMessage> chat,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Live (streaming) chat: emits the assistant's reply token-by-token.
    /// </summary>
    /// <remarks>
    /// Streaming with tool execution is intentionally <b>not supported</b> on this
    /// API — pass <see cref="ChatAsync{TResponse}"/> with tools for tool-driven
    /// runs (and use <c>GenerateStreamAsync</c> for fine-grained agent events).
    /// </remarks>
    IAsyncEnumerable<string> ChatStreamAsync(
        ILlm llm,
        IPrompt prompt,
        IReadOnlyList<ChatMessage> chat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Plain-text overload of <see cref="ChatStreamAsync(ILlm, IPrompt, IReadOnlyList{ChatMessage}, CancellationToken)"/>.
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        ILlm llm,
        string systemPrompt,
        IReadOnlyList<ChatMessage> chat,
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
    Task<Result<float[]>> GenerateAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default);

    #endregion

    #region Audio

    /// <summary>
    /// Transcribes audio to text.
    /// </summary>
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
    Task<ResultAgent<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Agent-mode overload for plain-text output (no structured response type).
    /// </summary>
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
    IAsyncEnumerable<AgentEvent> GenerateStreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams an agent run that resumes from an existing <paramref name="chat"/>
    /// transcript. The model sees the prior conversation as session state, then
    /// continues with full tool-calling capability — events are emitted exactly
    /// as for the no-chat overload.
    /// </summary>
    /// <remarks>
    /// This is the streaming counterpart of <c>ChatAsync</c> with tools — useful
    /// for live UI: subscribe to <see cref="AgentFinalTextEvent"/> for the final
    /// text and to <see cref="AgentToolCallStartedEvent"/> /
    /// <see cref="AgentToolCallCompletedEvent"/> for tool activity (with parallel
    /// fan-out preserved).
    /// </remarks>
    IAsyncEnumerable<AgentEvent> GenerateStreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
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

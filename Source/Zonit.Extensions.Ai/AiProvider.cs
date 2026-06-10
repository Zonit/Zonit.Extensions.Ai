using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Default AI provider implementation that routes requests to registered model providers.
/// </summary>
internal sealed class AiProvider : IAiProvider
{
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly AgentRunner _agentRunner;
    private readonly IPromptRenderer _renderer;
    private readonly AiUsageTracker _tracker;
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly ILogger<AiProvider> _logger;
    private readonly IServiceProvider _serviceProvider;

    public AiProvider(
        IEnumerable<IModelProvider> providers,
        AgentRunner agentRunner,
        IPromptRenderer renderer,
        AiUsageTracker tracker,
        IOptions<AiOptions> aiOptions,
        ILogger<AiProvider> logger,
        IServiceProvider serviceProvider)
    {
        _providers = providers;
        _agentRunner = agentRunner;
        _renderer = renderer;
        _tracker = tracker;
        _aiOptions = aiOptions;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    #region Fluent

    /// <inheritdoc />
    public IAgentRequest<TResponse> Agent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm, IPrompt<TResponse> prompt)
        => new AgentRequest<TResponse>(
            _serviceProvider,
            (tools, mcps, options, context, ct) => GenerateAsync(llm, prompt, tools, mcps, options, context, ct),
            (tools, mcps, options, context, ct) => GenerateStreamAsync(llm, prompt, tools, mcps, options, context, ct));

    /// <inheritdoc />
    public IAgentRequest<string> Agent(IAgentLlm llm, string prompt)
        => new AgentRequest<string>(
            _serviceProvider,
            (tools, mcps, options, context, ct) => GenerateAsync(llm, prompt, tools, mcps, options, context, ct),
            (_, _, _, _, _) => throw new NotSupportedException(
                "Streaming a string-prompt agent is not supported. Use Agent(llm, IPrompt<TResponse>) to stream."));

    /// <inheritdoc />
    public IChatRequest<TResponse> Chat<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm, IPrompt<TResponse> prompt, IReadOnlyList<ChatMessage> history)
        => new ChatRequest<TResponse>(
            _serviceProvider,
            (tools, mcps, options, context, ct) => ChatWithToolsAsync(llm, prompt, history, tools, mcps, options, context, ct),
            (tools, mcps, options, context, ct) => ChatStreamWithToolsAsync(llm, prompt, history, tools, mcps, options, context, ct));

    /// <inheritdoc />
    public IChatRequest<string> Chat(ILlm llm, string systemPrompt, IReadOnlyList<ChatMessage> history)
        => new ChatRequest<string>(
            _serviceProvider,
            (tools, mcps, options, context, ct) => ChatWithToolsAsync<string>(llm, new SimplePrompt<string>(systemPrompt ?? string.Empty), history, tools, mcps, options, context, ct),
            (tools, mcps, options, context, ct) => ChatStreamWithToolsAsync<string>(llm, new SimplePrompt<string>(systemPrompt ?? string.Empty), history, tools, mcps, options, context, ct));

    #endregion

    /// <summary>
    /// Wraps a single-shot leaf operation so that, when it runs <i>inside</i> an active
    /// tracking scope (i.e. a tool of some agent called us), its usage/cost/output is
    /// recorded as a child of that scope. At the top level (no active scope) this is a
    /// no-op passthrough — no allocation, no behavior change for plain single-shot calls.
    /// </summary>
    private async Task<Result<T>> TrackedLeafAsync<T>(
        AiUsageKind kind,
        ILlm llm,
        string providerName,
        string? inputText,
        Func<Task<Result<T>>> operation)
    {
        if (!_tracker.IsTracking)
            return await operation().ConfigureAwait(false);

        var capture = _aiOptions.Value.Agent?.CaptureNestedIo ?? true;
        var scope = _tracker.BeginScope(kind, llm.Name, providerName);
        try
        {
            var result = await operation().ConfigureAwait(false);
            _tracker.Record(
                scope,
                result.MetaData.Usage,
                result.MetaData.Duration,
                result.MetaData.RequestId,
                input: capture ? UsageText.Preview(inputText) : null,
                output: capture ? UsageText.Describe(result.Value) : null);
            return result;
        }
        finally
        {
            _tracker.EndScope(scope);
        }
    }

    /// <summary>
    /// Renders <paramref name="prompt"/>'s raw template (if any) and returns a wrapper
    /// whose <c>Text</c> is the rendered string. Providers always observe rendered text.
    /// </summary>
    /// <remarks>
    /// The wrapper carries the original prompt's CLR type name forward so the
    /// agent runner can surface a user-facing <see cref="MetaData.PromptName"/>
    /// (e.g. <c>"Brief"</c>) instead of the framework wrapper's own type
    /// (<c>"RenderedPrompt`1"</c>). Without this, every metadata consumer sees
    /// the wrapper name and the original prompt identity is lost.
    /// </remarks>
    private IPrompt<TResponse> Materialize<TResponse>(IPrompt<TResponse> prompt)
    {
        // Already a non-template prompt (SimplePrompt et al.) — Text is final.
        if (prompt is not PromptBase)
            return prompt;

        var rendered = _renderer.Render(prompt);
        return new RenderedPrompt<TResponse>(rendered, prompt.Files, prompt.GetType().Name);
    }

    private IPrompt Materialize(IPrompt prompt)
    {
        if (prompt is not PromptBase)
            return prompt;

        var rendered = _renderer.Render(prompt);
        return new RenderedPrompt(rendered, prompt.Files, prompt.GetType().Name);
    }

    #region Text Generation

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        var materialized = Materialize(prompt);
        _logger.LogDebug("Generating with {Provider}/{Model}", provider.Name, llm.Name);

        return await TrackedLeafAsync(AiUsageKind.Generate, llm, provider.Name, materialized.Text,
            () => provider.GenerateAsync(llm, materialized, cancellationToken));
    }

    /// <inheritdoc />
    public Task<Result<string>> GenerateAsync(
        ILlm llm,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        return GenerateAsync<string>(llm, new SimplePrompt<string>(prompt), cancellationToken);
    }

    #endregion

    #region Chat

    /// <inheritdoc />
    public Task<Result<TResponse>> ChatAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        CancellationToken cancellationToken = default)
        => ChatWithToolsAsync(llm, prompt, chat, tools: null, mcps: null, options: null, context: null, cancellationToken);

    /// <inheritdoc />
    public Task<Result<string>> ChatAsync(
        ILlm llm,
        string systemPrompt,
        IReadOnlyList<ChatMessage> chat,
        CancellationToken cancellationToken = default)
        => ChatWithToolsAsync<string>(llm, new SimplePrompt<string>(systemPrompt ?? string.Empty), chat,
            tools: null, mcps: null, options: null, context: null, cancellationToken);

    /// <summary>
    /// Tool-driven chat — the engine behind the fluent <c>Chat(...)</c> builder. Not part of the
    /// public <see cref="IAiProvider"/> surface: callers reach it through <c>ai.Chat(...).RunAsync()</c>
    /// (with tools / context) or the plain <c>ChatAsync</c> overloads above (without).
    /// </summary>
    internal async Task<Result<TResponse>> ChatWithToolsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        IReadOnlyList<ITool>? tools,
        IReadOnlyList<Mcp>? mcps,
        AgentOptions? options,
        IReadOnlyList<object>? context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(chat);

        // Tool-driven chat → delegate to the agent runner with seeded history.
        // Without tools/mcps/options it's a single-shot chat call routed to the provider.
        if (tools is not null || mcps is not null || options is not null)
        {
            if (llm is not IAgentLlm agentLlm)
                throw new InvalidOperationException(
                    $"Chat with tools or MCP requires an IAgentLlm-capable model. " +
                    $"'{llm.GetType().Name}' is not agent-capable in this provider; " +
                    $"use a plain ai.Chat(llm, system, history).RunAsync() without tools.");

            _logger.LogDebug("Chat (agent) with {Model} ({Turns} seeded messages)", llm.Name, chat.Count);
            // ResultAgent<T> : Result<T> — assignable directly. Iterations / ToolCalls
            // / Request / Total / Usage remain accessible via a downcast if the caller
            // wants the full agent trace.
            return await _agentRunner
                .RunAsync(agentLlm, Materialize(prompt), tools, mcps, options, cancellationToken, chat, context)
                .ConfigureAwait(false);
        }

        var provider = GetProviderForModel(llm);
        var materialized = Materialize(prompt);
        _logger.LogDebug("Chat with {Provider}/{Model} ({Turns} messages)", provider.Name, llm.Name, chat.Count);
        return await TrackedLeafAsync(AiUsageKind.Chat, llm, provider.Name, materialized.Text,
            () => provider.ChatAsync(llm, materialized, chat, cancellationToken));
    }

    /// <summary>
    /// Streaming engine behind <c>ai.Chat(...).RunStreamAsync()</c>: resumes the agent loop from
    /// <paramref name="chat"/> and emits <see cref="AgentEvent"/>s. Requires an agent-capable model.
    /// </summary>
    internal IAsyncEnumerable<AgentEvent> ChatStreamWithToolsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        IReadOnlyList<ITool>? tools,
        IReadOnlyList<Mcp>? mcps,
        AgentOptions? options,
        IReadOnlyList<object>? context,
        CancellationToken cancellationToken)
    {
        if (llm is not IAgentLlm agentLlm)
            throw new NotSupportedException(
                $"Streaming a chat requires an agent-capable model (IAgentLlm); '{llm.GetType().Name}' is not. " +
                "For plain token-by-token streaming without tools use ai.ChatStreamAsync(llm, system, history).");

        return GenerateStreamAsync(agentLlm, prompt, chat, tools, mcps, options, context, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> ChatStreamAsync(
        ILlm llm,
        IPrompt prompt,
        IReadOnlyList<ChatMessage> chat,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(chat);

        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Chat stream with {Provider}/{Model} ({Turns} messages)", provider.Name, llm.Name, chat.Count);
        return provider.ChatStreamAsync(llm, Materialize(prompt), chat, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> ChatStreamAsync(
        ILlm llm,
        string systemPrompt,
        IReadOnlyList<ChatMessage> chat,
        CancellationToken cancellationToken = default)
        => ChatStreamAsync(llm, new SimplePrompt<string>(systemPrompt ?? string.Empty), chat, cancellationToken);

    #endregion

    #region Image Generation

    /// <inheritdoc />
    public async Task<Result<Asset>> GenerateAsync(
        IImageLlm llm,
        string description,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Generating image with {Provider}/{Model}", provider.Name, llm.Name);

        return await TrackedLeafAsync(AiUsageKind.Image, llm, provider.Name, description,
            () => provider.GenerateImageAsync(llm, new SimpleImagePrompt(description), cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<Asset>> GenerateAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Generating image with {Provider}/{Model}", provider.Name, llm.Name);

        return await TrackedLeafAsync(AiUsageKind.Image, llm, provider.Name, prompt.Text,
            () => provider.GenerateImageAsync(llm, Materialize(prompt), cancellationToken));
    }

    #endregion

    #region Video Generation

    /// <inheritdoc />
    public async Task<Result<Asset>> GenerateAsync(
        IVideoLlm llm,
        string description,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Generating video with {Provider}/{Model}", provider.Name, llm.Name);

        return await TrackedLeafAsync(AiUsageKind.Video, llm, provider.Name, description,
            () => provider.GenerateVideoAsync(llm, new SimpleImagePrompt(description), cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<Asset>> GenerateAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Generating video with {Provider}/{Model}", provider.Name, llm.Name);

        return await TrackedLeafAsync(AiUsageKind.Video, llm, provider.Name, prompt.Text,
            () => provider.GenerateVideoAsync(llm, Materialize(prompt), cancellationToken));
    }

    #endregion

    #region Embeddings

    /// <inheritdoc />
    public async Task<Result<float[]>> GenerateAsync(
        IEmbeddingLlm llm,
        string input,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Embedding with {Provider}/{Model}", provider.Name, llm.Name);

        return await TrackedLeafAsync(AiUsageKind.Embed, llm, provider.Name, input,
            () => provider.EmbedAsync(llm, input, cancellationToken));
    }

    #endregion

    #region Audio

    /// <inheritdoc />
    public async Task<Result<string>> GenerateAsync(
        IAudioLlm llm,
        Asset audioFile,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Transcribing with {Provider}/{Model}", provider.Name, llm.Name);

        return await TrackedLeafAsync(AiUsageKind.Audio, llm, provider.Name, null,
            () => provider.TranscribeAsync(llm, audioFile, language, cancellationToken));
    }

    #endregion

    #region Streaming

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamAsync(
        ILlm llm,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Streaming with {Provider}/{Model}", provider.Name, llm.Name);

        return provider.StreamAsync<string>(llm, Materialize<string>(new SimplePrompt<string>(prompt)), cancellationToken);
    }

    #endregion

    #region Agent (internal — driven through the fluent IAgentRequest builder)

    /// <summary>Agent run engine behind <c>ai.Agent(...).RunAsync()</c>. Not on the public IAiProvider surface.</summary>
    internal Task<ResultAgent<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        IReadOnlyList<object>? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Agent run with {Model}", llm.Name);
        return _agentRunner.RunAsync(llm, Materialize(prompt), tools, mcps, options, cancellationToken, initialChat: null, callerContext: context);
    }

    /// <summary>Plain-text agent run engine behind <c>ai.Agent(llm, string).RunAsync()</c>.</summary>
    internal Task<ResultAgent<string>> GenerateAsync(
        IAgentLlm llm,
        string prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        IReadOnlyList<object>? context = null,
        CancellationToken cancellationToken = default)
    {
        return GenerateAsync(llm, new SimplePrompt<string>(prompt), tools, mcps, options, context, cancellationToken);
    }

    /// <summary>Agent stream engine behind <c>ai.Agent(...).RunStreamAsync()</c>.</summary>
    internal IAsyncEnumerable<AgentEvent> GenerateStreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        IReadOnlyList<object>? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Agent stream with {Model}", llm.Name);
        return _agentRunner.StreamAsync(llm, Materialize(prompt), tools, mcps, options, cancellationToken, initialChat: null, callerContext: context);
    }

    /// <summary>Agent stream (resumed from chat history) engine behind <c>ai.Chat(...).RunStreamAsync()</c>.</summary>
    internal IAsyncEnumerable<AgentEvent> GenerateStreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        IReadOnlyList<object>? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Agent stream (chat) with {Model} ({Turns} seeded messages)", llm.Name, chat.Count);
        return _agentRunner.StreamAsync(llm, Materialize(prompt), tools, mcps, options, cancellationToken, chat, context);
    }

    #endregion

    #region Cost Calculation

    /// <inheritdoc />
    public Price CalculateCost(ILlm llm, int inputTokens, int outputTokens)
    {
        var inputPrice = llm.GetInputPrice(inputTokens);
        var outputPrice = llm.GetOutputPrice(outputTokens);

        var inputCost = (inputTokens / 1_000_000m) * inputPrice;
        var outputCost = (outputTokens / 1_000_000m) * outputPrice;

        return new Price(inputCost + outputCost);
    }

    /// <inheritdoc />
    public Price CalculateCost(IImageLlm llm, int imageCount = 1)
    {
        return new Price(llm.PriceOutput * imageCount);
    }

    /// <inheritdoc />
    public Price CalculateCost(IEmbeddingLlm llm, int inputTokens)
    {
        var inputCost = (inputTokens / 1_000_000m) * llm.PriceInput;
        return new Price(inputCost);
    }

    /// <inheritdoc />
    public Price CalculateCost(IAudioLlm llm, int durationSeconds)
    {
        var minutes = durationSeconds / 60.0m;
        return new Price(llm.PriceInput * minutes);
    }

    /// <inheritdoc />
    public Price CalculateCost(IVideoLlm llm, int durationSeconds, int videoCount = 1)
    {
        // Use the model's built-in pricing calculation
        return new Price(llm.GetVideoGenerationPrice() * videoCount);
    }

    /// <inheritdoc />
    public Price EstimateCost(ILlm llm, string promptText, int estimatedOutputTokens = 500)
    {
        var estimatedInputTokens = (promptText.Length / 4) + 10;
        return CalculateCost(llm, estimatedInputTokens, estimatedOutputTokens);
    }

    #endregion

    private IModelProvider GetProviderForModel(ILlm llm)
    {
        var provider = _providers.FirstOrDefault(p => p.SupportsModel(llm));

        if (provider is null)
        {
            var available = string.Join(", ", _providers.Select(p => p.Name));
            throw new InvalidOperationException(
                $"No provider found for model '{llm.Name}'. " +
                $"Available providers: [{available}]. " +
                $"Make sure you've installed the correct provider package (e.g., Zonit.Extensions.Ai.OpenAi).");
        }

        return provider;
    }
}

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Default AI provider implementation that routes requests to registered model providers.
/// </summary>
internal sealed class AiProvider : IAiProvider
{
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly AgentRunner _agentRunner;
    private readonly ILogger<AiProvider> _logger;

    public AiProvider(
        IEnumerable<IModelProvider> providers,
        AgentRunner agentRunner,
        ILogger<AiProvider> logger)
    {
        _providers = providers;
        _agentRunner = agentRunner;
        _logger = logger;
    }

    #region Text Generation

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Generating with {Provider}/{Model}", provider.Name, llm.Name);

        return await provider.GenerateAsync(llm, prompt, cancellationToken);
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
    public async Task<Result<TResponse>> ChatAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default)
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
                    $"omit tools/mcps/options for a plain chat completion.");

            _logger.LogDebug("Chat (agent) with {Model} ({Turns} seeded messages)", llm.Name, chat.Count);
            // ResultAgent<T> : Result<T> — assignable directly. Iterations / ToolCalls
            // / TotalUsage / TotalCost remain accessible via a downcast if the caller
            // wants the full agent trace.
            return await _agentRunner
                .RunAsync(agentLlm, prompt, tools, mcps, options, cancellationToken, chat)
                .ConfigureAwait(false);
        }

        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Chat with {Provider}/{Model} ({Turns} messages)", provider.Name, llm.Name, chat.Count);
        return await provider.ChatAsync(llm, prompt, chat, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<string>> ChatAsync(
        ILlm llm,
        string systemPrompt,
        IReadOnlyList<ChatMessage> chat,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default)
        => ChatAsync<string>(llm, new SimplePrompt<string>(systemPrompt ?? string.Empty), chat, tools, mcps, options, cancellationToken);

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
        return provider.ChatStreamAsync(llm, prompt, chat, cancellationToken);
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

        return await provider.GenerateImageAsync(llm, new SimpleImagePrompt(description), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<Asset>> GenerateAsync(
        IImageLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Generating image with {Provider}/{Model}", provider.Name, llm.Name);

        return await provider.GenerateImageAsync(llm, prompt, cancellationToken);
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

        return await provider.GenerateVideoAsync(llm, new SimpleImagePrompt(description), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<Asset>> GenerateAsync(
        IVideoLlm llm,
        IPrompt<Asset> prompt,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderForModel(llm);
        _logger.LogDebug("Generating video with {Provider}/{Model}", provider.Name, llm.Name);

        return await provider.GenerateVideoAsync(llm, prompt, cancellationToken);
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

        return await provider.EmbedAsync(llm, input, cancellationToken);
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

        return await provider.TranscribeAsync(llm, audioFile, language, cancellationToken);
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

        return provider.StreamAsync<string>(llm, new SimplePrompt<string>(prompt), cancellationToken);
    }

    #endregion

    #region Agent

    /// <inheritdoc />
    public Task<ResultAgent<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Agent run with {Model}", llm.Name);
        return _agentRunner.RunAsync(llm, prompt, tools, mcps, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ResultAgent<string>> GenerateAsync(
        IAgentLlm llm,
        string prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GenerateAsync(llm, new SimplePrompt<string>(prompt), tools, mcps, options, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<AgentEvent> GenerateStreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Agent stream with {Model}", llm.Name);
        return _agentRunner.StreamAsync(llm, prompt, tools, mcps, options, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<AgentEvent> GenerateStreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ChatMessage> chat,
        IReadOnlyList<ITool>? tools = null,
        IReadOnlyList<Mcp>? mcps = null,
        AgentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Agent stream (chat) with {Model} ({Turns} seeded messages)", llm.Name, chat.Count);
        return _agentRunner.StreamAsync(llm, prompt, tools, mcps, options, cancellationToken, chat);
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

using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Default AI provider implementation that routes requests to registered model providers.
/// </summary>
internal sealed class AiProvider : IAiProvider
{
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly ILogger<AiProvider> _logger;

    public AiProvider(IEnumerable<IModelProvider> providers, ILogger<AiProvider> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    #region Text Generation

    /// <inheritdoc />
    public async Task<Result<TResponse>> GenerateAsync<TResponse>(
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

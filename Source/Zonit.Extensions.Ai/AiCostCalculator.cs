using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Calculates costs for AI operations based on token usage and model pricing.
/// Returns Price (always non-negative) since costs cannot be negative.
/// </summary>
public static class AiCostCalculator
{
    /// <summary>
    /// Calculates the input cost for a text generation operation.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="cachedTokens">Number of cached tokens (cheaper).</param>
    /// <returns>Input cost as Price.</returns>
    public static Price CalculateInputCost(ILlm llm, int inputTokens, int cachedTokens = 0)
    {
        var inputPrice = llm.GetInputPrice(inputTokens);

        // Prices are per 1M tokens
        var inputCost = (inputTokens / 1_000_000m) * inputPrice;

        // Apply cached tokens discount if model supports it
        if (cachedTokens > 0 && llm is ITextLlm textLlm && textLlm.PriceCachedInput.HasValue)
        {
            var cachedInputPrice = textLlm.PriceCachedInput.Value;
            var cachedCost = (cachedTokens / 1_000_000m) * cachedInputPrice;
            var nonCachedTokens = inputTokens - cachedTokens;
            inputCost = (nonCachedTokens / 1_000_000m) * inputPrice + cachedCost;
        }

        return new Price(inputCost);
    }

    /// <summary>
    /// Calculates the output cost for a text generation operation.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <returns>Output cost as Price.</returns>
    public static Price CalculateOutputCost(ILlm llm, int outputTokens)
    {
        var outputPrice = llm.GetOutputPrice(outputTokens);
        var outputCost = (outputTokens / 1_000_000m) * outputPrice;
        return new Price(outputCost);
    }

    /// <summary>
    /// Calculates the total cost for a text generation operation.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="usage">Token usage from the operation.</param>
    /// <returns>Total cost as Price.</returns>
    public static Price CalculateCost(ILlm llm, TokenUsage usage)
    {
        var inputCost = CalculateInputCost(llm, usage.InputTokens, usage.CachedTokens);
        var outputCost = CalculateOutputCost(llm, usage.OutputTokens);
        return inputCost + outputCost;
    }

    /// <summary>
    /// Calculates input and output costs separately.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="usage">Token usage from the operation.</param>
    /// <returns>Tuple of (InputCost, OutputCost).</returns>
    public static (Price InputCost, Price OutputCost) CalculateCosts(ILlm llm, TokenUsage usage)
    {
        var inputCost = CalculateInputCost(llm, usage.InputTokens, usage.CachedTokens);
        var outputCost = CalculateOutputCost(llm, usage.OutputTokens);
        return (inputCost, outputCost);
    }

    /// <summary>
    /// Calculates the total cost for a batch operation.
    /// Batch operations typically have 50% discount.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="usage">Token usage from the operation.</param>
    /// <returns>Total cost as Price.</returns>
    public static Price CalculateBatchCost(ILlm llm, TokenUsage usage)
    {
        var inputPrice = llm.BatchPriceInput ?? llm.PriceInput * 0.5m;
        var outputPrice = llm.BatchPriceOutput ?? llm.PriceOutput * 0.5m;

        var inputCost = (usage.InputTokens / 1_000_000m) * inputPrice;
        var outputCost = (usage.OutputTokens / 1_000_000m) * outputPrice;

        return new Price(inputCost + outputCost);
    }

    /// <summary>
    /// Calculates the cost for image generation.
    /// </summary>
    /// <param name="llm">The image model used.</param>
    /// <param name="imageCount">Number of images generated.</param>
    /// <returns>Total cost as Price.</returns>
    public static Price CalculateImageCost(IImageLlm llm, int imageCount = 1)
    {
        // Image models typically have per-image pricing
        return new Price(llm.PriceOutput * imageCount);
    }

    /// <summary>
    /// Calculates the cost for embedding operations.
    /// </summary>
    /// <param name="llm">The embedding model used.</param>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <returns>Total cost as Price.</returns>
    public static Price CalculateEmbeddingCost(IEmbeddingLlm llm, int inputTokens)
    {
        var inputCost = (inputTokens / 1_000_000m) * llm.PriceInput;
        return new Price(inputCost);
    }

    /// <summary>
    /// Calculates the cost for audio transcription.
    /// </summary>
    /// <param name="llm">The audio model used.</param>
    /// <param name="durationSeconds">Audio duration in seconds.</param>
    /// <returns>Total cost as Price.</returns>
    public static Price CalculateAudioCost(IAudioLlm llm, int durationSeconds)
    {
        // Audio models typically charge per minute
        var minutes = durationSeconds / 60.0m;
        return new Price(llm.PriceInput * minutes);
    }

    /// <summary>
    /// Estimates the cost for a prompt before sending.
    /// Uses approximate token count (4 chars = 1 token).
    /// </summary>
    /// <param name="llm">The language model to use.</param>
    /// <param name="promptText">The prompt text.</param>
    /// <param name="estimatedOutputTokens">Estimated output tokens.</param>
    /// <returns>Estimated cost as Price.</returns>
    public static Price EstimateCost(ILlm llm, string promptText, int estimatedOutputTokens = 500)
    {
        var estimatedInputTokens = (promptText.Length / 4) + 10; // Add buffer

        var inputCost = (estimatedInputTokens / 1_000_000m) * llm.PriceInput;
        var outputCost = (estimatedOutputTokens / 1_000_000m) * llm.PriceOutput;

        return new Price(inputCost + outputCost);
    }
}

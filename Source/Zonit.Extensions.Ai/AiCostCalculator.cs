using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Calculates costs for AI operations based on token usage and model pricing.
/// Returns Price (always non-negative) since costs cannot be negative.
/// </summary>
public static class AiCostCalculator
{
    /// <summary>
    /// Picks the rate card a request is billed on: the model's standard prices, or the fast-tier
    /// prices from <see cref="IFast"/>.
    /// </summary>
    /// <remarks>
    /// The single place that decision is made. The model publishes both cards and never applies
    /// the premium itself, because only the caller knows whether the fast tier was actually
    /// served — OpenAI and xAI downgrade to standard scheduling when their fast capacity is short
    /// and charge accordingly. Returns <c>null</c> when standard prices apply, so every rate
    /// lookup below reads as "fast price if there is one, otherwise the standard one".
    /// </remarks>
    private static IFast? FastRates(ILlm llm, bool fastGranted)
        => fastGranted && llm is IFast { Speed: SpeedType.Fast } fast ? fast : null;

    /// <summary>
    /// Calculates the input cost for a text generation operation.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="cachedTokens">Number of cached tokens (cheaper).</param>
    /// <param name="cacheWriteTokens">Number of cache-write tokens (typically more expensive than regular input).</param>
    /// <param name="fastGranted">See <see cref="CalculateCosts"/>.</param>
    /// <returns>Input cost as Price.</returns>
    public static Price CalculateInputCost(
        ILlm llm,
        int inputTokens,
        int cachedTokens = 0,
        int cacheWriteTokens = 0,
        bool fastGranted = true)
    {
        var fast = FastRates(llm, fastGranted);

        var inputPrice = fast?.GetFastInputPrice(inputTokens) ?? llm.GetInputPrice(inputTokens);

        // Split input into: regular | cache reads | cache writes — each may have a
        // different price. No ITextLlm gate here: IReasoningLlm carries its own
        // PriceCachedInput without deriving from ITextLlm, so gating on the marker
        // interface billed every reasoning model's cache reads at the full input rate.
        var regularTokens = Math.Max(0, inputTokens - cachedTokens - cacheWriteTokens);
        var inputCost = (regularTokens / 1_000_000m) * inputPrice;

        // Cache reads: cheaper (e.g. 0.1× base price). Priced through the getter so
        // long-context tiers surcharge cache reads too (OpenAI GPT-5.6: 2× past 272K).
        if (cachedTokens > 0)
        {
            var readPrice = fast?.GetFastCachedInputPrice(inputTokens) ?? llm.GetCachedInputPrice(inputTokens);
            inputCost += (cachedTokens / 1_000_000m) * readPrice;
        }

        // Cache writes: more expensive (e.g. 1.25× base price for Anthropic 5-min TTL)
        if (cacheWriteTokens > 0)
        {
            var writePrice = fast?.GetFastCachedInputWritePrice(inputTokens) ?? llm.GetCachedInputWritePrice(inputTokens);
            inputCost += (cacheWriteTokens / 1_000_000m) * writePrice;
        }

        return new Price(inputCost);
    }

    /// <summary>
    /// Calculates the output cost for a text generation operation.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="inputTokens">
    /// Number of input (context) tokens. Required because long-context surcharges
    /// on the OUTPUT rate are keyed on the input size (OpenAI GPT-5.6 bills output
    /// at 1.5× once input exceeds 272K).
    /// </param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <param name="fastGranted">See <see cref="CalculateCosts"/>.</param>
    /// <returns>Output cost as Price.</returns>
    public static Price CalculateOutputCost(
        ILlm llm,
        int inputTokens,
        int outputTokens,
        bool fastGranted = true)
    {
        var fast = FastRates(llm, fastGranted);

        var outputPrice = fast?.GetFastOutputPrice(inputTokens, outputTokens)
            ?? llm.GetOutputPrice(inputTokens, outputTokens);

        var outputCost = (outputTokens / 1_000_000m) * outputPrice;
        return new Price(outputCost);
    }

    /// <summary>
    /// Calculates the total cost for a text generation operation.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="usage">Token usage from the operation.</param>
    /// <param name="fastGranted">See <see cref="CalculateCosts"/>.</param>
    /// <returns>Total cost as Price.</returns>
    public static Price CalculateCost(ILlm llm, TokenUsage usage, bool fastGranted = true)
    {
        var (inputCost, outputCost) = CalculateCosts(llm, usage, fastGranted);
        return inputCost + outputCost;
    }

    /// <summary>
    /// Calculates input and output costs separately.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="usage">Token usage from the operation.</param>
    /// <param name="fastGranted">
    /// Whether a requested fast tier (<see cref="IFast"/> with <see cref="SpeedType.Fast"/>) was
    /// actually served. Pass what the provider echoed back (<c>AiFastTier.WasGranted</c>): OpenAI
    /// and xAI both downgrade to standard scheduling when their fast capacity is exhausted, and
    /// only charge the premium when they confirm it. Ignored for models that did not ask for fast
    /// mode; defaults to <c>true</c> so callers that cannot observe the tier bill what they asked
    /// for.
    /// </param>
    /// <returns>Tuple of (InputCost, OutputCost).</returns>
    public static (Price InputCost, Price OutputCost) CalculateCosts(ILlm llm, TokenUsage usage, bool fastGranted = true)
    {
        var inputCost = CalculateInputCost(
            llm, usage.InputTokens, usage.CachedTokens, usage.CacheWriteTokens, fastGranted);
        var outputCost = CalculateOutputCost(llm, usage.InputTokens, usage.OutputTokens, fastGranted);

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
        var inputPrice = llm.GetBatchInputPrice(usage.InputTokens);
        var outputPrice = llm.GetBatchOutputPrice(usage.InputTokens, usage.OutputTokens);

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
        // Each model calculates its own price based on quality and size
        return new Price(llm.GetImageGenerationPrice() * imageCount);
    }

    /// <summary>
    /// Calculates the input and output cost for an image generation billed per
    /// token (OpenAI <c>gpt-image-2</c> / <c>gpt-image-2.5</c>) rather than at a
    /// flat per-image rate.
    /// </summary>
    /// <param name="llm">The token-priced image model used.</param>
    /// <param name="usage">Token breakdown reported by the endpoint.</param>
    /// <returns>Tuple of (InputCost, OutputCost).</returns>
    public static (Price InputCost, Price OutputCost) CalculateImageTokenCosts(
        ITokenPricedImageLlm llm,
        ImageTokenUsage usage)
    {
        // Cached counts are a subset of their input counts (OpenAI convention),
        // so the uncached remainder is what gets billed at the full rate.
        var regularText = Math.Max(0, usage.TextInputTokens - usage.CachedTextInputTokens);
        var regularImage = Math.Max(0, usage.ImageInputTokens - usage.CachedImageInputTokens);

        var inputCost =
            (regularText / 1_000_000m) * llm.PriceTextInput +
            (usage.CachedTextInputTokens / 1_000_000m) * llm.PriceCachedTextInput +
            (regularImage / 1_000_000m) * llm.PriceImageInput +
            (usage.CachedImageInputTokens / 1_000_000m) * llm.PriceCachedImageInput;

        var outputCost = (usage.ImageOutputTokens / 1_000_000m) * llm.PriceImageOutput;

        return (new Price(inputCost), new Price(outputCost));
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

        // No response to read a tier from yet, so a fast model estimates at its fast rates —
        // the tier the caller asked for is the honest pre-flight assumption.
        return CalculateCost(llm, new TokenUsage
        {
            InputTokens = estimatedInputTokens,
            OutputTokens = estimatedOutputTokens,
        });
    }
}

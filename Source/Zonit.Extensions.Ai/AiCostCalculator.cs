using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Calculates costs for AI operations based on token usage and model pricing.
/// Returns Price (always non-negative) since costs cannot be negative.
/// </summary>
public static class AiCostCalculator
{
    /// <summary>
    /// The tier a request is billed on, from the legacy "was the requested tier granted" flag:
    /// the requested tier when granted, standard otherwise.
    /// </summary>
    private static SpeedType TierFrom(ILlm llm, bool fastGranted)
        => fastGranted ? AiFastTier.RequestedOrStandard(llm) : SpeedType.Standard;

    /// <summary>
    /// The tier actually billed: the served tier, capped at what the model offers — a tier the
    /// model has no rate card for (<see cref="IFast"/> / <see cref="IUltraFast"/>) bills at the
    /// highest one it has.
    /// </summary>
    /// <remarks>
    /// The rate-card decision is made in this file only. The model publishes every card and never
    /// applies a premium itself, because only the caller knows which tier was actually served —
    /// OpenAI and xAI downgrade to standard scheduling when premium capacity is short and charge
    /// accordingly.
    /// </remarks>
    private static SpeedType Billable(ILlm llm, SpeedType served) => served switch
    {
        SpeedType.UltraFast when llm is IUltraFast => SpeedType.UltraFast,
        SpeedType.UltraFast or SpeedType.Fast when llm is IFast => SpeedType.Fast,
        _ => SpeedType.Standard,
    };

    private static decimal InputRate(ILlm llm, SpeedType tier, long inputTokens) => tier switch
    {
        SpeedType.UltraFast => ((IUltraFast)llm).GetUltraFastInputPrice(inputTokens),
        SpeedType.Fast => ((IFast)llm).GetFastInputPrice(inputTokens),
        _ => llm.GetInputPrice(inputTokens),
    };

    private static decimal CachedReadRate(ILlm llm, SpeedType tier, long inputTokens) => tier switch
    {
        SpeedType.UltraFast => ((IUltraFast)llm).GetUltraFastCachedInputPrice(inputTokens),
        SpeedType.Fast => ((IFast)llm).GetFastCachedInputPrice(inputTokens),
        _ => llm.GetCachedInputPrice(inputTokens),
    };

    private static decimal CachedWriteRate(ILlm llm, SpeedType tier, long inputTokens) => tier switch
    {
        SpeedType.UltraFast => ((IUltraFast)llm).GetUltraFastCachedInputWritePrice(inputTokens),
        SpeedType.Fast => ((IFast)llm).GetFastCachedInputWritePrice(inputTokens),
        _ => llm.GetCachedInputWritePrice(inputTokens),
    };

    private static decimal OutputRate(ILlm llm, SpeedType tier, long inputTokens, long outputTokens) => tier switch
    {
        SpeedType.UltraFast => ((IUltraFast)llm).GetUltraFastOutputPrice(inputTokens, outputTokens),
        SpeedType.Fast => ((IFast)llm).GetFastOutputPrice(inputTokens, outputTokens),
        _ => llm.GetOutputPrice(inputTokens, outputTokens),
    };

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
        => CalculateInputCost(llm, inputTokens, cachedTokens, cacheWriteTokens, TierFrom(llm, fastGranted));

    /// <summary>
    /// Calculates the input cost for a text generation operation billed on the tier the provider
    /// served (see <see cref="AiFastTier.Served"/>).
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="cachedTokens">Number of cached tokens (cheaper).</param>
    /// <param name="cacheWriteTokens">Number of cache-write tokens.</param>
    /// <param name="servedSpeed">The speed tier the request was served on.</param>
    /// <returns>Input cost as Price.</returns>
    public static Price CalculateInputCost(
        ILlm llm,
        int inputTokens,
        int cachedTokens,
        int cacheWriteTokens,
        SpeedType servedSpeed)
    {
        var tier = Billable(llm, servedSpeed);

        var inputPrice = InputRate(llm, tier, inputTokens);

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
            var readPrice = CachedReadRate(llm, tier, inputTokens);
            inputCost += (cachedTokens / 1_000_000m) * readPrice;
        }

        // Cache writes: more expensive (e.g. 1.25× base price for Anthropic 5-min TTL)
        if (cacheWriteTokens > 0)
        {
            var writePrice = CachedWriteRate(llm, tier, inputTokens);
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
        => CalculateOutputCost(llm, inputTokens, outputTokens, TierFrom(llm, fastGranted));

    /// <summary>
    /// Calculates the output cost for a text generation operation billed on the tier the provider
    /// served (see <see cref="AiFastTier.Served"/>).
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="inputTokens">Number of input (context) tokens — long-context tiers key on it.</param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <param name="servedSpeed">The speed tier the request was served on.</param>
    /// <returns>Output cost as Price.</returns>
    public static Price CalculateOutputCost(
        ILlm llm,
        int inputTokens,
        int outputTokens,
        SpeedType servedSpeed)
    {
        var outputPrice = OutputRate(llm, Billable(llm, servedSpeed), inputTokens, outputTokens);

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
        => CalculateCosts(llm, usage, TierFrom(llm, fastGranted));

    /// <summary>
    /// Calculates input and output costs separately, billed on the tier the provider served.
    /// </summary>
    /// <param name="llm">The language model used.</param>
    /// <param name="usage">Token usage from the operation.</param>
    /// <param name="servedSpeed">
    /// The speed tier the request was actually served on (<see cref="AiFastTier.Served"/>). A
    /// tier the model has no rate card for bills at the highest tier it does have.
    /// </param>
    /// <returns>Tuple of (InputCost, OutputCost).</returns>
    public static (Price InputCost, Price OutputCost) CalculateCosts(ILlm llm, TokenUsage usage, SpeedType servedSpeed)
    {
        var inputCost = CalculateInputCost(
            llm, usage.InputTokens, usage.CachedTokens, usage.CacheWriteTokens, servedSpeed);
        var outputCost = CalculateOutputCost(llm, usage.InputTokens, usage.OutputTokens, servedSpeed);

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

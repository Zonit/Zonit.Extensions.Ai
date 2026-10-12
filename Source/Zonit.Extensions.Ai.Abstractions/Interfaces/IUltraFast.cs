namespace Zonit.Extensions.Ai;

/// <summary>
/// Implemented by models that offer an <b>ultrafast</b> tier on top of fast mode — OpenAI
/// <c>service_tier: "ultrafast"</c> on <c>gpt-6.1-sol</c> and <c>gpt-6-astra</c>. Setting
/// <see cref="IFast.Speed"/> to <see cref="SpeedType.UltraFast"/> opts a request in.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="IFast"/>, this is a rate card, not a decision: whether the ultrafast prices are
/// charged is decided when the cost is computed, from the tier the provider reports it served.
/// </para>
/// <para>
/// OpenAI prices ultrafast at a flat 6× the standard card on every token type, long context
/// included, so a model normally implements nothing beyond the interface itself. Override
/// <see cref="UltraFastMultiplier"/> for a different uniform factor, or the individual
/// <c>GetUltraFast*Price</c> members for a non-uniform card.
/// </para>
/// </remarks>
public interface IUltraFast : IFast
{
    /// <summary>
    /// How much more the ultrafast tier costs than the standard one, when the premium is uniform
    /// across every token type. Default: <c>6</c> — OpenAI's ultrafast factor.
    /// </summary>
    decimal UltraFastMultiplier => 6m;

    /// <summary>Headline price per 1M input tokens in ultrafast mode (short context).</summary>
    decimal UltraFastPriceInput => PriceInput * UltraFastMultiplier;

    /// <summary>Headline price per 1M output tokens in ultrafast mode (short context).</summary>
    decimal UltraFastPriceOutput => PriceOutput * UltraFastMultiplier;

    /// <summary>Ultrafast input price for a request of this size (long-context tiers compose with the premium).</summary>
    decimal GetUltraFastInputPrice(long inputTokens) => GetInputPrice(inputTokens) * UltraFastMultiplier;

    /// <summary>Ultrafast output price for a request of this size, keyed on the input size.</summary>
    decimal GetUltraFastOutputPrice(long inputTokens, long outputTokens)
        => GetOutputPrice(inputTokens, outputTokens) * UltraFastMultiplier;

    /// <summary>Ultrafast cache-read price for a request of this size.</summary>
    decimal GetUltraFastCachedInputPrice(long inputTokens)
        => GetCachedInputPrice(inputTokens) * UltraFastMultiplier;

    /// <summary>Ultrafast cache-write price for a request of this size.</summary>
    decimal GetUltraFastCachedInputWritePrice(long inputTokens)
        => GetCachedInputWritePrice(inputTokens) * UltraFastMultiplier;
}

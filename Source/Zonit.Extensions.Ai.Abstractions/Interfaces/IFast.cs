namespace Zonit.Extensions.Ai;

/// <summary>
/// Implemented by models that offer a faster inference tier at premium pricing — Anthropic fast
/// mode (<c>speed: "fast"</c>), OpenAI fast mode and xAI Priority Processing (both
/// <c>service_tier</c>). Setting <see cref="Speed"/> to <see cref="SpeedType.Fast"/> opts a
/// request in; the provider translates it to its own wire value.
/// </summary>
/// <remarks>
/// <para>
/// <b>This interface is a rate card, not a decision.</b> The model publishes two sets of prices —
/// the standard ones on <see cref="ILlm"/> and the fast ones here — and <b>which set is used is
/// decided when the cost is computed</b>, not inside the model. That matters because fast mode is
/// best-effort: OpenAI and xAI downgrade a request to standard scheduling when their fast capacity
/// is exhausted, echo the tier they actually served, and only charge the premium when they confirm
/// it. A model that baked the premium into its own <see cref="ILlm.GetInputPrice"/> could not
/// express "asked for fast, was served standard" — it would over-report every downgraded request.
/// See <c>AiCostCalculator.CalculateCosts(llm, usage, fastGranted)</c>.
/// </para>
/// <para>
/// <b>The simple case is free.</b> Every provider that ships fast mode today prices it at a flat
/// 2× on every token type, so a model normally implements nothing but <see cref="Speed"/> and
/// inherits the rest — including long-context tiering, because the defaults multiply the model's
/// own tier-aware getters rather than a flat headline rate. Override <see cref="FastMultiplier"/>
/// for a different uniform factor.
/// </para>
/// <para>
/// <b>Non-uniform fast pricing is supported too.</b> A model whose fast card is not a single
/// multiple of its standard card overrides the individual <c>GetFast*Price</c> members that
/// differ — for example a tier that doubles output but leaves cache reads alone. Override the
/// headline <see cref="FastPriceInput"/> / <see cref="FastPriceOutput"/> alongside them so the
/// documented short-context rates stay in step with what is billed.
/// </para>
/// </remarks>
public interface IFast : ILlm
{
    /// <summary>
    /// Selected inference speed. Defaults to <see cref="SpeedType.Standard"/>;
    /// set to <see cref="SpeedType.Fast"/> to opt the request into fast mode.
    /// </summary>
    SpeedType Speed { get; }

    /// <summary>
    /// How much more the fast tier costs than the standard one, when the premium is uniform
    /// across every token type. Default: <c>2</c> — the factor every provider charges today.
    /// Ignored by any rate whose <c>GetFast*Price</c> member the model overrides.
    /// </summary>
    decimal FastMultiplier => 2m;

    /// <summary>Headline price per 1M input tokens in fast mode (short context).</summary>
    decimal FastPriceInput => PriceInput * FastMultiplier;

    /// <summary>Headline price per 1M output tokens in fast mode (short context).</summary>
    decimal FastPriceOutput => PriceOutput * FastMultiplier;

    /// <summary>
    /// Fast-tier input price for a request of this size. Defaults to the standard tier-aware
    /// price scaled by <see cref="FastMultiplier"/>, so long-context surcharges compose with the
    /// fast premium instead of being lost.
    /// </summary>
    decimal GetFastInputPrice(long inputTokens) => GetInputPrice(inputTokens) * FastMultiplier;

    /// <summary>
    /// Fast-tier output price for a request of this size. Keyed on the input size, like
    /// <see cref="ILlm.GetOutputPrice"/>.
    /// </summary>
    decimal GetFastOutputPrice(long inputTokens, long outputTokens)
        => GetOutputPrice(inputTokens, outputTokens) * FastMultiplier;

    /// <summary>Fast-tier cache-read price for a request of this size.</summary>
    decimal GetFastCachedInputPrice(long inputTokens)
        => GetCachedInputPrice(inputTokens) * FastMultiplier;

    /// <summary>Fast-tier cache-write price for a request of this size.</summary>
    decimal GetFastCachedInputWritePrice(long inputTokens)
        => GetCachedInputWritePrice(inputTokens) * FastMultiplier;
}

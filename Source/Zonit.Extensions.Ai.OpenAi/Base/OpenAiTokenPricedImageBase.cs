namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Base class for OpenAI image models billed per token rather than at a flat
/// per-image rate — the scheme introduced with <c>gpt-image-2</c> and carried
/// over to the <c>gpt-image-2.5</c> family.
/// </summary>
/// <remarks>
/// <para>
/// All three models share one rate card (per 1M tokens): text input $5.00
/// ($1.25 cached), image input $8.00 ($2.00 cached), image output $30.00.
/// </para>
/// <para>
/// The real cost of a call comes from the endpoint's <c>usage</c> block and is
/// reported in <c>MetaData.Usage</c>. <see cref="GetImageGenerationPrice"/> is
/// only a pre-flight estimate, derived from <see cref="EstimatedOutputTokens"/>.
/// </para>
/// </remarks>
/// <typeparam name="TQuality">The model-specific quality enum type.</typeparam>
/// <typeparam name="TSize">The model-specific size enum type.</typeparam>
public abstract class OpenAiTokenPricedImageBase<TQuality, TSize>
    : OpenAiImageBase<TQuality, TSize>, ITokenPricedImageLlm
    where TQuality : struct, Enum
    where TSize : struct, Enum
{
    /// <inheritdoc />
    public virtual decimal PriceTextInput => 5.00m;

    /// <inheritdoc />
    public virtual decimal PriceCachedTextInput => 1.25m;

    /// <inheritdoc />
    public virtual decimal PriceImageInput => 8.00m;

    /// <inheritdoc />
    public virtual decimal PriceCachedImageInput => 2.00m;

    /// <inheritdoc />
    public virtual decimal PriceImageOutput => 30.00m;

    /// <summary>
    /// Text input rate, so the generic <see cref="ILlm"/> surface reports the
    /// prompt-side price rather than zero.
    /// </summary>
    public override decimal PriceInput => PriceTextInput;

    /// <summary>
    /// Generated-image rate. Note this is per 1M output tokens, NOT per image —
    /// use <see cref="GetImageGenerationPrice"/> for a per-image figure.
    /// </summary>
    public override decimal PriceOutput => PriceImageOutput;

    /// <summary>
    /// Approximate number of generated image tokens for the configured quality
    /// and size. Used only for the pre-flight estimate below.
    /// </summary>
    protected abstract int EstimatedOutputTokens { get; }

    /// <summary>
    /// Estimated price of one image, from <see cref="EstimatedOutputTokens"/> at
    /// the image-output rate. Billing uses the endpoint's reported tokens, so
    /// this is an approximation for budgeting, not the amount charged.
    /// </summary>
    /// <returns>Estimated price in dollars for generating one image.</returns>
    public override decimal GetImageGenerationPrice()
        => (EstimatedOutputTokens / 1_000_000m) * PriceImageOutput;
}

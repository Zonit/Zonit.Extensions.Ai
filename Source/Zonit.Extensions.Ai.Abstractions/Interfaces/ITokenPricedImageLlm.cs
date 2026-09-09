namespace Zonit.Extensions.Ai;

/// <summary>
/// Image model billed per token instead of at a flat per-image rate — the model
/// OpenAI moved to with <c>gpt-image-2</c> and <c>gpt-image-2.5</c>. Text and
/// image tokens are priced separately on input, and the generated image is
/// billed as output tokens.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IImageLlm.GetImageGenerationPrice"/> stays on these models as a
/// pre-flight ESTIMATE (used by <c>IAiProvider.CalculateCost(IImageLlm, int)</c>
/// when no request has run yet). The billed cost reported in
/// <c>MetaData.Usage</c> is computed from the endpoint's real token counts via
/// <c>AiCostCalculator.CalculateImageTokenCosts</c>, so it does not depend on
/// that estimate.
/// </para>
/// <para>
/// Rates are per 1M tokens, matching the rest of the pricing surface.
/// </para>
/// </remarks>
public interface ITokenPricedImageLlm : IImageLlm
{
    /// <summary>Price per 1M text prompt tokens.</summary>
    decimal PriceTextInput { get; }

    /// <summary>Price per 1M cached text prompt tokens.</summary>
    decimal PriceCachedTextInput { get; }

    /// <summary>Price per 1M input image tokens (reference images, edit sources, masks).</summary>
    decimal PriceImageInput { get; }

    /// <summary>Price per 1M cached input image tokens.</summary>
    decimal PriceCachedImageInput { get; }

    /// <summary>Price per 1M generated image tokens.</summary>
    decimal PriceImageOutput { get; }
}

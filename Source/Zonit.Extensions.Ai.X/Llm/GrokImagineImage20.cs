namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok Imagine Image 2.0 — xAI's current text-to-image and image-to-image
/// model. Adds 2K output, quality tiers and a much wider aspect-ratio set on
/// top of the original <see cref="GrokImagineImage"/>.
/// </summary>
/// <remarks>
/// Model id <c>grok-imagine-image-2.0</c>. $0.04 per image regardless of
/// resolution or quality. Available in us-east-1 and us-west-2, and supported
/// by the Batch API. See https://docs.x.ai/developers/models/grok-imagine-image-2.0
/// </remarks>
public class GrokImagineImage20 : XImagineImageBase
{
    /// <inheritdoc />
    public override string Name => "grok-imagine-image-2.0";

    /// <inheritdoc />
    public override decimal PriceInput => 0.00m; // Text input is free

    /// <inheritdoc />
    public override decimal PriceOutput => 0.04m; // $0.04 per image

    /// <summary>
    /// Calculates the price for generating a single image.
    /// </summary>
    /// <returns>Price in dollars for generating one image ($0.04).</returns>
    public override decimal GetImageGenerationPrice() => 0.04m;
}

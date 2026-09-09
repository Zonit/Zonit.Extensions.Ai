namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok Imagine Image Quality — the quality-tuned Grok Imagine image model,
/// for final assets where fidelity matters more than throughput. For everyday
/// generation use <see cref="GrokImagineImage20"/>.
/// </summary>
/// <remarks>
/// Model id <c>grok-imagine-image-quality</c>. $0.05 per image regardless of
/// resolution or quality setting.
/// </remarks>
public class GrokImagineImageQuality : XImagineImageBase
{
    /// <inheritdoc />
    public override string Name => "grok-imagine-image-quality";

    /// <inheritdoc />
    public override decimal PriceInput => 0.00m; // Text input is free

    /// <inheritdoc />
    public override decimal PriceOutput => 0.05m; // $0.05 per image

    /// <summary>
    /// Calculates the price for generating a single image.
    /// </summary>
    /// <returns>Price in dollars for generating one image ($0.05).</returns>
    public override decimal GetImageGenerationPrice() => 0.05m;
}

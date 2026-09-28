namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok Imagine Image Quality — the quality-tuned Grok Imagine image model,
/// for final assets where fidelity matters more than throughput. For everyday
/// generation use <see cref="GrokImagineImage20"/>.
/// </summary>
/// <remarks>
/// Model id <c>grok-imagine-image-quality</c>. $0.05 per image regardless of
/// resolution or quality setting. xAI retires the model on 2 November 2026: from
/// then on requests are served by <c>grok-imagine-image-2.0</c> at
/// <c>quality: "low"</c> (see <see href="https://docs.x.ai/developers/migration/imagine-image-quality-nov-2"/>).
/// </remarks>
[Obsolete("grok-imagine-image-quality is retired by xAI on 2026-11-02 (requests are then redirected to grok-imagine-image-2.0 at low quality). Migrate to GrokImagineImage20.")]
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

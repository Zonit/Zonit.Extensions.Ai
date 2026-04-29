namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Image 2 — OpenAI's next-generation image generation model.
/// Adds higher-resolution presets (2K, 4K, ultrawide) on top of the
/// 1024 / 1536 sizes shared with earlier models.
/// </summary>
/// <remarks>
/// <para>
/// <b>Size constraints (model-specific):</b>
/// <list type="bullet">
///   <item><description>Maximum edge length must be ≤ 3840 px.</description></item>
///   <item><description>Both edges must be multiples of 16 px.</description></item>
///   <item><description>Long-edge to short-edge ratio must not exceed 3:1.</description></item>
///   <item><description>Total pixels must be ≥ 655 360 and ≤ 8 294 400.</description></item>
/// </list>
/// Outputs above 2 560 × 1 440 (≈ 3.7 MP, "2K") are flagged experimental by OpenAI.
/// </para>
/// <para>
/// Pricing in <see cref="GetImageGenerationPrice"/> is a placeholder mirroring
/// the GPT Image 1.5 schedule until OpenAI publishes the official table for
/// gpt-image-2; adjust when the docs land.
/// </para>
/// </remarks>
public class GPTImage2 : OpenAiImageBase<GPTImage2.QualityType, GPTImage2.SizeType>
{
    /// <summary>
    /// Image quality setting.
    /// </summary>
    public required override QualityType Quality { get; init; }

    /// <summary>
    /// Image size / dimensions.
    /// </summary>
    public required override SizeType Size { get; init; }

    /// <inheritdoc />
    public override string Name => "gpt-image-2";

    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Image;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures => FeaturesType.Inpainting;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Image |
        EndpointsType.ImageEdit;

    /// <summary>
    /// Calculates the price for generating a single image based on quality and size.
    /// 2K and 4K presets are estimated at 2× / 4× the 1024-tier price respectively
    /// pending OpenAI's official table.
    /// </summary>
    /// <returns>Price in dollars for generating one image.</returns>
    public override decimal GetImageGenerationPrice()
    {
        return (Quality, Size) switch
        {
            // Low quality
            (QualityType.Low, SizeType.Square) => 0.015m,
            (QualityType.Low, SizeType.Landscape) => 0.020m,
            (QualityType.Low, SizeType.Portrait) => 0.020m,
            (QualityType.Low, SizeType.Square2K) => 0.030m,
            (QualityType.Low, SizeType.Landscape2K) => 0.040m,
            (QualityType.Low, SizeType.Landscape4K) => 0.080m,
            (QualityType.Low, SizeType.Portrait4K) => 0.080m,
            (QualityType.Low, SizeType.Auto) => 0.015m,

            // Medium quality
            (QualityType.Medium, SizeType.Square) => 0.050m,
            (QualityType.Medium, SizeType.Landscape) => 0.075m,
            (QualityType.Medium, SizeType.Portrait) => 0.075m,
            (QualityType.Medium, SizeType.Square2K) => 0.100m,
            (QualityType.Medium, SizeType.Landscape2K) => 0.150m,
            (QualityType.Medium, SizeType.Landscape4K) => 0.300m,
            (QualityType.Medium, SizeType.Portrait4K) => 0.300m,
            (QualityType.Medium, SizeType.Auto) => 0.050m,

            // High quality
            (QualityType.High, SizeType.Square) => 0.200m,
            (QualityType.High, SizeType.Landscape) => 0.300m,
            (QualityType.High, SizeType.Portrait) => 0.300m,
            (QualityType.High, SizeType.Square2K) => 0.400m,
            (QualityType.High, SizeType.Landscape2K) => 0.600m,
            (QualityType.High, SizeType.Landscape4K) => 1.200m,
            (QualityType.High, SizeType.Portrait4K) => 1.200m,
            (QualityType.High, SizeType.Auto) => 0.200m,

            // Auto quality — falls back to medium-tier pricing.
            (QualityType.Auto, SizeType.Square) => 0.050m,
            (QualityType.Auto, SizeType.Landscape) => 0.075m,
            (QualityType.Auto, SizeType.Portrait) => 0.075m,
            (QualityType.Auto, SizeType.Square2K) => 0.100m,
            (QualityType.Auto, SizeType.Landscape2K) => 0.150m,
            (QualityType.Auto, SizeType.Landscape4K) => 0.300m,
            (QualityType.Auto, SizeType.Portrait4K) => 0.300m,
            (QualityType.Auto, SizeType.Auto) => 0.050m,

            _ => throw new ArgumentException($"Unknown combination of quality ({Quality}) and size ({Size})")
        };
    }

    /// <summary>
    /// Image quality settings for GPT Image 2.
    /// </summary>
    public enum QualityType
    {
        /// <summary>Let the model choose the best quality.</summary>
        [EnumValue("auto")]
        Auto,

        /// <summary>
        /// Low quality — fastest option, ideal for drafts, thumbnails, and
        /// quick iterations before committing to medium / high for final assets.
        /// </summary>
        [EnumValue("low")]
        Low,

        /// <summary>Medium quality, balanced.</summary>
        [EnumValue("medium")]
        Medium,

        /// <summary>High quality, best details.</summary>
        [EnumValue("high")]
        High,
    }

    /// <summary>
    /// Image size presets for GPT Image 2. Any resolution that satisfies the
    /// model's edge / ratio / pixel constraints is technically accepted by the
    /// API; the enum exposes the popular sizes documented by OpenAI.
    /// </summary>
    public enum SizeType
    {
        /// <summary>Let the model choose the best size.</summary>
        [EnumValue("auto")]
        Auto,

        /// <summary>Square format (1024 × 1024).</summary>
        [EnumValue("1024x1024")]
        Square,

        /// <summary>Landscape format (1536 × 1024).</summary>
        [EnumValue("1536x1024")]
        Landscape,

        /// <summary>Portrait format (1024 × 1536).</summary>
        [EnumValue("1024x1536")]
        Portrait,

        /// <summary>2K square (2048 × 2048). Experimental tier (&gt; 2 560 × 1 440 total pixels).</summary>
        [EnumValue("2048x2048")]
        Square2K,

        /// <summary>2K landscape (2048 × 1152). Experimental tier.</summary>
        [EnumValue("2048x1152")]
        Landscape2K,

        /// <summary>4K landscape (3840 × 2160). Experimental tier — at maximum edge length.</summary>
        [EnumValue("3840x2160")]
        Landscape4K,

        /// <summary>4K portrait (2160 × 3840). Experimental tier — at maximum edge length.</summary>
        [EnumValue("2160x3840")]
        Portrait4K,
    }
}

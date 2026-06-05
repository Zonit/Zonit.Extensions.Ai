namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok Imagine Image - X's image generation model.
/// Generates images from text prompts with configurable aspect ratio.
/// </summary>
/// <remarks>
/// For more information, see: https://docs.x.ai/docs/models/grok-imagine-image
/// Pricing: $0.02 per image (1024x1024)
/// Image input: $0.002 per image
/// Note: X.ai does not support quality parameter - only aspect_ratio is used.
/// </remarks>
public class GrokImagineImage : XBase, IImageLlm
{
    /// <summary>
    /// Image aspect ratio setting. Default is 1:1 (square).
    /// </summary>
    public AspectRatioType AspectRatio { get; init; } = AspectRatioType.Ratio1x1;

    /// <inheritdoc />
    public override string Name => "grok-imagine-image";

    /// <inheritdoc />
    public override decimal PriceInput => 0.00m; // Text input is free

    /// <inheritdoc />
    public override decimal PriceOutput => 0.02m; // $0.02 per image

    /// <inheritdoc />
    public override int MaxInputTokens => 32_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0; // Image generation doesn't use output tokens

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Image;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures => FeaturesType.None;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Image;

    /// <summary>
    /// Gets the quality value for API request.
    /// Note: X.ai does not support quality parameter, this is kept for interface compatibility.
    /// </summary>
    public string QualityValue => "standard";

    /// <summary>
    /// Gets the size value for API request.
    /// Note: X.ai does not require size - use AspectRatio instead.
    /// </summary>
    public string SizeValue => "1024x1024";

    /// <summary>
    /// Gets the aspect ratio value for API request.
    /// </summary>
    public string AspectRatioValue => AspectRatio.GetEnumValue();

    /// <summary>
    /// Calculates the price for generating a single image.
    /// </summary>
    /// <returns>Price in dollars for generating one image ($0.02).</returns>
    public decimal GetImageGenerationPrice() => 0.02m;

    /// <summary>
    /// Image aspect ratio settings for Grok Imagine Image.
    /// Default is 1:1 (square).
    /// </summary>
    public enum AspectRatioType
    {
        /// <summary>1:1 aspect ratio (square, default).</summary>
        [EnumValue("1:1")]
        Ratio1x1,

        /// <summary>16:9 aspect ratio (landscape).</summary>
        [EnumValue("16:9")]
        Ratio16x9,

        /// <summary>4:3 aspect ratio (classic TV).</summary>
        [EnumValue("4:3")]
        Ratio4x3,

        /// <summary>9:16 aspect ratio (portrait/vertical).</summary>
        [EnumValue("9:16")]
        Ratio9x16,

        /// <summary>3:4 aspect ratio (portrait).</summary>
        [EnumValue("3:4")]
        Ratio3x4,

        /// <summary>3:2 aspect ratio (photo).</summary>
        [EnumValue("3:2")]
        Ratio3x2,

        /// <summary>2:3 aspect ratio (portrait photo).</summary>
        [EnumValue("2:3")]
        Ratio2x3
    }
}

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Image 1.5 - State-of-the-art image generation model.
/// </summary>
public class GPTImage15 : OpenAiImageBase<GPTImage15.QualityType, GPTImage15.SizeType>
{
    /// <summary>
    /// Image quality setting.
    /// </summary>
    public required override QualityType Quality { get; init; }

    /// <summary>
    /// Image size/dimensions.
    /// </summary>
    public required override SizeType Size { get; init; }

    /// <inheritdoc />
    public override string Name => "gpt-image-1.5";

    // Text tokens pricing per 1M tokens
    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.00m; // No output text tokens for image generation

    // Note: GPT Image 1.5 pricing (estimated, adjust as needed)
    // Image generation pricing (per image):
    // Low quality: $0.015 (1024x1024), $0.020 (1024x1536, 1536x1024)
    // Medium quality: $0.050 (1024x1024), $0.075 (1024x1536, 1536x1024)
    // High quality: $0.200 (1024x1024), $0.300 (1024x1536, 1536x1024)

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000; // Context window

    /// <inheritdoc />
    public override int MaxOutputTokens => 0; // Image generation doesn't use output tokens

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
    /// </summary>
    /// <returns>Price in dollars for generating one image.</returns>
    public override decimal GetImageGenerationPrice()
    {
        return (Quality, Size) switch
        {
            // Low quality pricing
            (QualityType.Low, SizeType.Square) => 0.015m,
            (QualityType.Low, SizeType.Portrait) => 0.020m,
            (QualityType.Low, SizeType.Landscape) => 0.020m,

            // Medium quality pricing
            (QualityType.Medium, SizeType.Square) => 0.050m,
            (QualityType.Medium, SizeType.Portrait) => 0.075m,
            (QualityType.Medium, SizeType.Landscape) => 0.075m,

            // High quality pricing
            (QualityType.High, SizeType.Square) => 0.200m,
            (QualityType.High, SizeType.Portrait) => 0.300m,
            (QualityType.High, SizeType.Landscape) => 0.300m,

            // Auto quality - use medium as default
            (QualityType.Auto, SizeType.Auto) => 0.050m,
            (QualityType.Auto, SizeType.Square) => 0.050m,
            (QualityType.Auto, SizeType.Portrait) => 0.075m,
            (QualityType.Auto, SizeType.Landscape) => 0.075m,

            // Auto size with specific quality
            (QualityType.Low, SizeType.Auto) => 0.015m,
            (QualityType.Medium, SizeType.Auto) => 0.050m,
            (QualityType.High, SizeType.Auto) => 0.200m,

            _ => throw new ArgumentException($"Unknown combination of quality ({Quality}) and size ({Size})")
        };
    }

    /// <summary>
    /// Image quality settings for GPT Image 1.5.
    /// </summary>
    public enum QualityType
    {
        /// <summary>Let the model choose the best quality.</summary>
        [EnumValue("auto")]
        Auto,

        /// <summary>Low quality, fastest generation.</summary>
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
    /// Image size settings for GPT Image 1.5.
    /// </summary>
    public enum SizeType
    {
        /// <summary>Let the model choose the best size.</summary>
        [EnumValue("auto")]
        Auto,

        /// <summary>Square format (1024x1024).</summary>
        [EnumValue("1024x1024")]
        Square,

        /// <summary>Landscape format (1536x1024).</summary>
        [EnumValue("1536x1024")]
        Landscape,

        /// <summary>Portrait format (1024x1536).</summary>
        [EnumValue("1024x1536")]
        Portrait
    }
}

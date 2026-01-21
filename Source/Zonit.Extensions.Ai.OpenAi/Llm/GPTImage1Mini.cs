namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Image 1 Mini - A cost-efficient version of GPT Image 1.
/// </summary>
public class GPTImage1Mini : OpenAiImageBase<GPTImage1Mini.QualityType, GPTImage1Mini.SizeType>
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
    public override string Name => "gpt-image-1-mini";

    // Text tokens pricing per 1M tokens
    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.00m; // No output text tokens for image generation

    // Note: Actual pricing is per image based on size and quality (see documentation)
    // Image token pricing:
    // Input: $2.50/1M image tokens, Cached: $0.25/1M image tokens
    // Output: $8.00/1M image tokens
    // Image generation pricing (per image):
    // Low quality: $0.005 (1024x1024), $0.006 (1024x1536, 1536x1024)
    // Medium quality: $0.011 (1024x1024), $0.015 (1024x1536, 1536x1024)
    // High quality: $0.036 (1024x1024), $0.052 (1024x1536, 1536x1024)

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
    public override FeaturesType SupportedFeatures => FeaturesType.None;

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
            (QualityType.Low, SizeType.Square) => 0.005m,
            (QualityType.Low, SizeType.Portrait) => 0.006m,
            (QualityType.Low, SizeType.Landscape) => 0.006m,

            // Medium quality pricing
            (QualityType.Medium, SizeType.Square) => 0.011m,
            (QualityType.Medium, SizeType.Portrait) => 0.015m,
            (QualityType.Medium, SizeType.Landscape) => 0.015m,

            // High quality pricing
            (QualityType.High, SizeType.Square) => 0.036m,
            (QualityType.High, SizeType.Portrait) => 0.052m,
            (QualityType.High, SizeType.Landscape) => 0.052m,

            _ => throw new ArgumentException($"Unknown combination of quality ({Quality}) and size ({Size})")
        };
    }

    /// <summary>
    /// Image quality settings for GPT Image 1 Mini.
    /// Note: No Auto option - explicit quality required.
    /// </summary>
    public enum QualityType
    {
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
    /// Image size settings for GPT Image 1 Mini.
    /// Note: No Auto option - explicit size required.
    /// </summary>
    public enum SizeType
    {
        /// <summary>Square format (1024x1024).</summary>
        [EnumValue("1024x1024")]
        Square,

        /// <summary>Portrait format (1024x1536).</summary>
        [EnumValue("1024x1536")]
        Portrait,

        /// <summary>Landscape format (1536x1024).</summary>
        [EnumValue("1536x1024")]
        Landscape
    }
}

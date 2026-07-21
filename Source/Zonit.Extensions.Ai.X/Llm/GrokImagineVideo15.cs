namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok Imagine Video 1.5 - X's current standard video generation model.
/// Supports both text-to-video (from a text prompt) and image-to-video
/// (animate a still image guided by a text prompt).
/// </summary>
/// <remarks>
/// <para>
/// The text <c>prompt</c> is the primary driver of what the video depicts; an
/// optional source image only provides the starting frame for image-to-video.
/// Unlike the older <see cref="GrokImagineVideo"/>, the 1.5 model does not
/// support video-to-video / reference-to-video guidance.
/// </para>
/// <para>
/// For more information, see: https://docs.x.ai/docs/models/grok-imagine-video
/// </para>
/// <para>
/// Maximum duration is 15 seconds. Image input is billed at $0.01/image; text
/// input is free. Output pricing per second:
/// 480p = $0.08, 720p = $0.14, 1080p = $0.25.
/// </para>
/// </remarks>
public class GrokImagineVideo15 : XBase, IVideoLlm
{
    /// <summary>
    /// Maximum supported video duration in seconds for this model.
    /// </summary>
    public const int MaxDurationSeconds = 15;

    /// <summary>
    /// Minimum supported video duration in seconds.
    /// </summary>
    public const int MinDurationSeconds = 1;

    private int _durationSeconds = 5;

    /// <summary>
    /// Video resolution setting.
    /// </summary>
    public required ResolutionType Resolution { get; init; }

    /// <summary>
    /// Video aspect ratio setting. Default is 16:9.
    /// </summary>
    public AspectRatioType AspectRatio { get; init; } = AspectRatioType.Ratio16x9;

    /// <summary>
    /// Video duration in seconds (1-15). Default is 5 seconds.
    /// Values outside range are clamped to valid range.
    /// </summary>
    public int DurationSeconds
    {
        get => _durationSeconds;
        init => _durationSeconds = Math.Clamp(value, MinDurationSeconds, MaxDurationSeconds);
    }

    /// <inheritdoc />
    public override string Name => "grok-imagine-video-1.5";

    /// <inheritdoc />
    public override decimal PriceInput => 0.00m; // Text input is free

    /// <inheritdoc />
    public override decimal PriceOutput => 0.08m; // Base price $0.08 per second (480p)

    /// <inheritdoc />
    public override int MaxInputTokens => 32_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0; // Video generation doesn't use output tokens

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Video;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures => FeaturesType.None;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Video;

    /// <summary>
    /// Gets the quality value for API request (resolution).
    /// </summary>
    public string QualityValue => Resolution.GetEnumValue();

    /// <summary>
    /// Gets the aspect ratio value for API request.
    /// </summary>
    public string AspectRatioValue => AspectRatio.GetEnumValue();

    /// <summary>
    /// Calculates the price for generating a single video.
    /// Pricing based on X/Grok API documentation.
    /// </summary>
    /// <returns>Price in dollars for generating one video.</returns>
    public decimal GetVideoGenerationPrice()
    {
        // Pricing per second based on resolution.
        var pricePerSecond = Resolution switch
        {
            ResolutionType.Resolution480p => 0.08m,
            ResolutionType.Resolution720p => 0.14m,
            ResolutionType.Resolution1080p => 0.25m,
            _ => 0.08m
        };

        return pricePerSecond * DurationSeconds;
    }

    /// <summary>
    /// Video resolution settings for Grok Imagine Video 1.5.
    /// </summary>
    public enum ResolutionType
    {
        /// <summary>480p resolution - $0.08 per second.</summary>
        [EnumValue("480p")]
        Resolution480p,

        /// <summary>720p resolution - $0.14 per second.</summary>
        [EnumValue("720p")]
        Resolution720p,

        /// <summary>1080p resolution - $0.25 per second.</summary>
        [EnumValue("1080p")]
        Resolution1080p
    }

    /// <summary>
    /// Video aspect ratio settings for Grok Imagine Video 1.5.
    /// Default is 16:9.
    /// </summary>
    public enum AspectRatioType
    {
        /// <summary>16:9 aspect ratio (landscape, default).</summary>
        [EnumValue("16:9")]
        Ratio16x9,

        /// <summary>4:3 aspect ratio (classic TV).</summary>
        [EnumValue("4:3")]
        Ratio4x3,

        /// <summary>1:1 aspect ratio (square).</summary>
        [EnumValue("1:1")]
        Ratio1x1,

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

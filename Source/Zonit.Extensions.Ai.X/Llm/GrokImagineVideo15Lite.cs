namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok Imagine Video 1.5 Lite - the low-cost variant of <see cref="GrokImagineVideo15"/>:
/// text-to-video and image-to-video at a quarter of the 480p price, with lower quality.
/// </summary>
/// <remarks>
/// <para>
/// Same endpoints as 1.5 (<c>/v1/videos/generations</c>, then polling). 1–15 seconds, 480p /
/// 720p / 1080p — Lite renders 1080p at 720p and upscales it, where 1.5 renders 1080p natively.
/// Reference-to-video, last-frame and keyframe guidance are 1.5 only. Batch API supported.
/// </para>
/// <para>
/// <b>Pricing.</b> xAI lists $0.020 per second, which is the 480p rate. xAI has not published
/// the 720p / 1080p rates; Artificial Analysis lists $8.40 per minute at 1080p ($0.14 per
/// second), and that figure is used for both 720p and 1080p (1080p is a 720p render), so cost is
/// over- rather than under-reported until xAI publishes them. For quality, Artificial Analysis
/// ranks it below 1.5 on its text-to-video board (Elo 993 vs 1045, October 2026).
/// </para>
/// <para>
/// For more information, see: https://docs.x.ai/developers/models/grok-imagine-video-1.5-lite
/// </para>
/// </remarks>
public class GrokImagineVideo15Lite : XBase, IVideoLlm
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
    public override string Name => "grok-imagine-video-1.5-lite";

    /// <inheritdoc />
    public override decimal PriceInput => 0.00m; // Text input is free

    /// <inheritdoc />
    public override decimal PriceOutput => 0.02m; // Base price $0.02 per second (480p)

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
            ResolutionType.Resolution480p => 0.02m,
            ResolutionType.Resolution720p => 0.14m, // unpublished; Artificial Analysis 1080p rate
            ResolutionType.Resolution1080p => 0.14m, // unpublished; Artificial Analysis
            _ => 0.02m
        };

        return pricePerSecond * DurationSeconds;
    }

    /// <summary>
    /// Video resolution settings for Grok Imagine Video 1.5 Lite.
    /// </summary>
    public enum ResolutionType
    {
        /// <summary>480p resolution - $0.02 per second.</summary>
        [EnumValue("480p")]
        Resolution480p,

        /// <summary>720p resolution - billed at $0.14 per second (rate not yet published by xAI).</summary>
        [EnumValue("720p")]
        Resolution720p,

        /// <summary>1080p resolution (720p render, upscaled) - billed at $0.14 per second (rate not yet published by xAI).</summary>
        [EnumValue("1080p")]
        Resolution1080p
    }

    /// <summary>
    /// Video aspect ratio settings for Grok Imagine Video 1.5 Lite.
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

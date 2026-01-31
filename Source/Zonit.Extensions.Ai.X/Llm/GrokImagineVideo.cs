namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok Imagine Video - X's video generation model.
/// Generates videos from text prompts with configurable duration and resolution.
/// </summary>
/// <remarks>
/// <para>
/// <strong>BETA STATUS:</strong> X.ai Video API is currently in beta. 
/// Video creation endpoint works but status polling may be unreliable.
/// </para>
/// <para>
/// For more information, see: https://docs.x.ai/docs/models/grok-imagine-video
/// </para>
/// <para>
/// Maximum duration is 15 seconds.
/// Pricing: 480p = $0.05/second, 720p = $0.07/second
/// </para>
/// </remarks>
public class GrokImagineVideo : XBase, IVideoLlm
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
    public override string Name => "grok-imagine-video";

    /// <inheritdoc />
    public override decimal PriceInput => 0.00m; // Text input is free

    /// <inheritdoc />
    public override decimal PriceOutput => 0.05m; // Base price $0.05 per second (480p)

    /// <inheritdoc />
    public override int MaxInputTokens => 32_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0; // Video generation doesn't use output tokens

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image | ChannelType.Video;

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
        // Pricing per second based on resolution
        var pricePerSecond = Resolution switch
        {
            ResolutionType.Resolution480p => 0.05m,
            ResolutionType.Resolution720p => 0.07m,
            _ => 0.05m
        };

        return pricePerSecond * DurationSeconds;
    }

    /// <summary>
    /// Video resolution settings for Grok Imagine Video.
    /// </summary>
    public enum ResolutionType
    {
        /// <summary>480p resolution - $0.05 per second.</summary>
        [EnumValue("480p")]
        Resolution480p,

        /// <summary>720p resolution - $0.07 per second.</summary>
        [EnumValue("720p")]
        Resolution720p
    }

    /// <summary>
    /// Video aspect ratio settings for Grok Imagine Video.
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

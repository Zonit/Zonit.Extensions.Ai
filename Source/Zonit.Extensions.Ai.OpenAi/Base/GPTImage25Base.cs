namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Shared surface of the GPT Image 2.5 family — <see cref="GPTImage25Sunburst"/>
/// (editing precision) and <see cref="GPTImage25Flare"/> (fast everyday
/// generation). Both models take the same parameters and the same rate card;
/// only the model id and the tuning differ.
/// </summary>
/// <remarks>
/// <para>
/// <b>Token-based pricing.</b> Unlike GPT Image 1.x, this generation is billed
/// per token (text input $5.00 / $1.25 cached, image input $8.00 / $2.00
/// cached, image output $30.00 per 1M) instead of at a flat per-image rate.
/// The cost reported in <c>MetaData.Usage</c> comes from the endpoint's own
/// token counts; <see cref="OpenAiTokenPricedImageBase{TQuality, TSize}.GetImageGenerationPrice"/>
/// is a pre-flight estimate only.
/// </para>
/// <para>
/// <b>Size constraints:</b> both edges a multiple of 16 px, long-edge to
/// short-edge ratio within 3:1, maximum edge 3840 px. The enum exposes the
/// presets OpenAI documents; any size satisfying the constraints is accepted
/// by the API.
/// </para>
/// </remarks>
public abstract class GPTImage25Base : OpenAiTokenPricedImageBase<GPTImage25Base.QualityType, GPTImage25Base.SizeType>
{
    /// <summary>Image quality setting.</summary>
    public required override QualityType Quality { get; init; }

    /// <summary>Image size / dimensions.</summary>
    public required override SizeType Size { get; init; }

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
    /// Estimated generated-image tokens for the configured quality and size.
    /// </summary>
    /// <remarks>
    /// OpenAI has not published a token table for GPT Image 2.5, so the figures
    /// are extrapolated from the published GPT Image 1 table (1024×1024: low
    /// 272, medium 1 056, high 4 160 tokens), which scales exactly with pixel
    /// area — the 1536×1024 row is 1.5× the square row at every quality. The
    /// <c>xhigh</c> and <c>max</c> tiers have no published anchor and are
    /// estimated at 1.5× and 2× the <c>high</c> tier. Treat the result as a
    /// budgeting figure; the amount actually charged is the token count the
    /// endpoint reports back.
    /// </remarks>
    protected override int EstimatedOutputTokens
        => (int)(QualityTokens(Quality) * AreaFactor(Size));

    /// <summary>Generated-image tokens at 1024 × 1024 for each quality tier.</summary>
    private static int QualityTokens(QualityType quality) => quality switch
    {
        QualityType.Low => 272,
        QualityType.Medium => 1_056,
        QualityType.High => 4_160,
        QualityType.XHigh => 6_240,
        QualityType.Max => 8_320,
        // Auto resolves per request; medium is the sensible middle for an estimate.
        QualityType.Auto => 1_056,
        _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, "Unknown quality tier")
    };

    /// <summary>Pixel area of each size preset relative to 1024 × 1024.</summary>
    private static decimal AreaFactor(SizeType size) => size switch
    {
        SizeType.Square => 1m,
        SizeType.Landscape => 1.5m,
        SizeType.Portrait => 1.5m,
        SizeType.Square2K => 4m,
        SizeType.Landscape2K => 2.25m,
        SizeType.Landscape4K => 7.91m,
        SizeType.Portrait4K => 7.91m,
        // Auto resolves per request; the square preset is the default shape.
        SizeType.Auto => 1m,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown size preset")
    };

    /// <summary>
    /// Image quality settings for GPT Image 2.5. Adds <see cref="XHigh"/> and
    /// <see cref="Max"/> above the tiers the 1.x models offered.
    /// </summary>
    public enum QualityType
    {
        /// <summary>Let the model choose the best quality.</summary>
        [EnumValue("auto")]
        Auto,

        /// <summary>Low quality — fastest, for drafts and thumbnails.</summary>
        [EnumValue("low")]
        Low,

        /// <summary>Medium quality, balanced.</summary>
        [EnumValue("medium")]
        Medium,

        /// <summary>High quality, good detail.</summary>
        [EnumValue("high")]
        High,

        /// <summary>Extra-high quality — above <see cref="High"/>, below <see cref="Max"/>.</summary>
        [EnumValue("xhigh")]
        XHigh,

        /// <summary>Maximum quality — slowest and most expensive.</summary>
        [EnumValue("max")]
        Max,
    }

    /// <summary>
    /// Image size presets for GPT Image 2.5.
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

        /// <summary>2K square (2048 × 2048).</summary>
        [EnumValue("2048x2048")]
        Square2K,

        /// <summary>2K landscape (2048 × 1152).</summary>
        [EnumValue("2048x1152")]
        Landscape2K,

        /// <summary>4K landscape (3840 × 2160) — at the maximum edge length.</summary>
        [EnumValue("3840x2160")]
        Landscape4K,

        /// <summary>4K portrait (2160 × 3840) — at the maximum edge length.</summary>
        [EnumValue("2160x3840")]
        Portrait4K,
    }
}

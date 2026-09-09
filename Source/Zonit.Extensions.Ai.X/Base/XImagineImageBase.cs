namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Base class for the current Grok Imagine image models
/// (<see cref="GrokImagineImage20"/>, <see cref="GrokImagineImageQuality"/>),
/// which extend the original <see cref="GrokImagineImage"/> surface with a
/// wider aspect-ratio set plus <c>resolution</c> and <c>quality</c> parameters.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Resolution"/> and <see cref="Quality"/> are optional: left unset
/// they are omitted from the request and the API applies its own defaults
/// (<c>1k</c> and <c>auto</c> respectively for grok-imagine-image-2.0).
/// </para>
/// <para>
/// Billing is a flat per-image rate, not per token — see
/// <see cref="IImageLlm.GetImageGenerationPrice"/> on each model.
/// </para>
/// </remarks>
public abstract class XImagineImageBase : XBase, IImageLlm
{
    /// <summary>
    /// Image aspect ratio. Defaults to <see cref="AspectRatioType.Auto"/>,
    /// which lets the model pick the best ratio for the prompt.
    /// </summary>
    public AspectRatioType AspectRatio { get; init; } = AspectRatioType.Auto;

    /// <summary>
    /// Output resolution. <c>null</c> omits the parameter (API default <c>1k</c>).
    /// </summary>
    public ResolutionType? Resolution { get; init; }

    /// <summary>
    /// Generation quality. <c>null</c> omits the parameter (API default <c>auto</c>).
    /// </summary>
    public QualityType? Quality { get; init; }

    /// <inheritdoc />
    public override int MaxInputTokens => 32_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0;

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
    /// Wire value for <c>quality</c>, or an empty string when unset — the
    /// provider omits the parameter in that case.
    /// </summary>
    public string QualityValue => Quality?.GetEnumValue() ?? string.Empty;

    /// <summary>
    /// Wire value for <c>resolution</c>, or an empty string when unset.
    /// </summary>
    public string ResolutionValue => Resolution?.GetEnumValue() ?? string.Empty;

    /// <summary>
    /// Not used by xAI, which sizes output through
    /// <see cref="AspectRatio"/> and <see cref="Resolution"/> instead.
    /// </summary>
    public string SizeValue => string.Empty;

    /// <inheritdoc />
    public string AspectRatioValue => AspectRatio.GetEnumValue();

    /// <inheritdoc />
    public abstract decimal GetImageGenerationPrice();

    /// <summary>
    /// Output resolution presets.
    /// </summary>
    public enum ResolutionType
    {
        /// <summary>1K output (API default).</summary>
        [EnumValue("1k")]
        Resolution1K,

        /// <summary>2K output.</summary>
        [EnumValue("2k")]
        Resolution2K,
    }

    /// <summary>
    /// Generation quality tiers. <c>medium</c> is the maximum xAI exposes.
    /// </summary>
    public enum QualityType
    {
        /// <summary>Let the model choose (API default).</summary>
        [EnumValue("auto")]
        Auto,

        /// <summary>Low quality — fastest.</summary>
        [EnumValue("low")]
        Low,

        /// <summary>Medium quality — the highest tier available.</summary>
        [EnumValue("medium")]
        Medium,
    }

    /// <summary>
    /// Aspect ratios accepted by the current Grok Imagine image models.
    /// </summary>
    public enum AspectRatioType
    {
        /// <summary>Let the model pick the best ratio for the prompt (default).</summary>
        [EnumValue("auto")]
        Auto,

        /// <summary>1:1 (square).</summary>
        [EnumValue("1:1")]
        Ratio1x1,

        /// <summary>16:9 (landscape).</summary>
        [EnumValue("16:9")]
        Ratio16x9,

        /// <summary>9:16 (portrait / vertical).</summary>
        [EnumValue("9:16")]
        Ratio9x16,

        /// <summary>4:3 (classic).</summary>
        [EnumValue("4:3")]
        Ratio4x3,

        /// <summary>3:4 (portrait).</summary>
        [EnumValue("3:4")]
        Ratio3x4,

        /// <summary>3:2 (photo).</summary>
        [EnumValue("3:2")]
        Ratio3x2,

        /// <summary>2:3 (portrait photo).</summary>
        [EnumValue("2:3")]
        Ratio2x3,

        /// <summary>2:1 (wide).</summary>
        [EnumValue("2:1")]
        Ratio2x1,

        /// <summary>1:2 (tall).</summary>
        [EnumValue("1:2")]
        Ratio1x2,

        /// <summary>19.5:9 (modern phone, landscape).</summary>
        [EnumValue("19.5:9")]
        Ratio19_5x9,

        /// <summary>9:19.5 (modern phone, portrait).</summary>
        [EnumValue("9:19.5")]
        Ratio9x19_5,

        /// <summary>20:9 (tall phone, landscape).</summary>
        [EnumValue("20:9")]
        Ratio20x9,

        /// <summary>9:20 (tall phone, portrait).</summary>
        [EnumValue("9:20")]
        Ratio9x20,

        /// <summary>21:9 (ultrawide).</summary>
        [EnumValue("21:9")]
        Ratio21x9,

        /// <summary>5:2 (panoramic).</summary>
        [EnumValue("5:2")]
        Ratio5x2,
    }
}

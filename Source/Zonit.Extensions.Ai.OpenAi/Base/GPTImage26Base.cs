namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Shared surface of the GPT Image 2.6 family — <see cref="GPTImage26Sunburst"/>
/// (editing precision) and <see cref="GPTImage26Flare"/> (fast everyday
/// generation), released 4 November 2026. Same rate card, quality tiers and size
/// presets as GPT Image 2.5 (see <see cref="GPTImage25Base"/>), plus a
/// <see cref="Complexity"/> setting and SVG / animated GIF output.
/// </summary>
/// <remarks>
/// <para>
/// <b>Token-based pricing</b> (per 1M tokens): text input $5.00 / $1.25 cached,
/// image input $8.00 / $2.00 cached, image output $30.00. The cost reported in
/// <c>MetaData.Usage</c> comes from the endpoint's token counts; the per-image
/// estimate uses the GPT Image 2.5 token table. OpenAI's own measurements are not
/// published, and 2.6 can spend noticeably more output tokens at low and medium
/// quality, so treat the estimate as a lower bound.
/// </para>
/// <para>
/// <b>Size constraints:</b> both edges a multiple of 16 px, aspect ratio within
/// 1:3 – 3:1, maximum edge 3840 px, total pixels between 655,360 and 8,294,400.
/// Sizes above 2560 × 1440 are experimental.
/// </para>
/// </remarks>
public abstract class GPTImage26Base : GPTImage25Base
{
    private int? _frameCount;
    private int? _frameDurationMs;

    /// <summary>
    /// How much detail and structure the model puts into the image, sent as
    /// <c>complexity</c>. Independent of <see cref="GPTImage25Base.Quality"/> and at no
    /// extra fee. <c>null</c> leaves the server default, <see cref="ComplexityType.High"/>.
    /// </summary>
    public ComplexityType? Complexity { get; init; }

    /// <summary>
    /// Output file format, sent as <c>output_format</c>. <c>null</c> leaves the server
    /// default (PNG). <see cref="OutputFormatType.Svg"/> returns static vector XML (the
    /// quality setting controls the raster reference image it is traced from);
    /// <see cref="OutputFormatType.Gif"/> returns an animation shaped by
    /// <see cref="FrameCount"/> and <see cref="FrameDurationMs"/>.
    /// </summary>
    public OutputFormatType? OutputFormat { get; init; }

    /// <summary>
    /// Number of GIF frames, 2–16 (server default 9). Only valid with
    /// <see cref="OutputFormatType.Gif"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Outside 2–16.</exception>
    public int? FrameCount
    {
        get => _frameCount;
        init
        {
            if (value is < 2 or > 16)
                throw new ArgumentOutOfRangeException(nameof(FrameCount), value, "GIF frame_count must be between 2 and 16.");
            _frameCount = value;
        }
    }

    /// <summary>
    /// Duration of each GIF frame in milliseconds, 20–2000 (server default 200,
    /// rounded to 10 ms). Only valid with <see cref="OutputFormatType.Gif"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Outside 20–2000.</exception>
    public int? FrameDurationMs
    {
        get => _frameDurationMs;
        init
        {
            if (value is < 20 or > 2000)
                throw new ArgumentOutOfRangeException(nameof(FrameDurationMs), value, "GIF frame_duration_ms must be between 20 and 2000.");
            _frameDurationMs = value;
        }
    }

    /// <summary>Text model that writes the SVG when <see cref="SvgModel"/> is left unset.</summary>
    public const string DefaultSvgModel = "gpt-6.1-sol";

    /// <summary>
    /// Model that authors the SVG, sent as <c>svg.model</c>. SVG output is two steps:
    /// the image model draws a raster reference, then this vision-capable text model
    /// writes the vector markup from it. The API requires it for SVG, so <c>null</c>
    /// sends <see cref="DefaultSvgModel"/>. Only valid with <see cref="OutputFormatType.Svg"/>.
    /// The author model's tokens are billed on its own rate card and are not included
    /// in the image cost estimate.
    /// </summary>
    public string? SvgModel { get; init; }

    /// <summary>
    /// Reasoning effort of the SVG author, sent as <c>svg.reasoning.effort</c>,
    /// independent of <see cref="Complexity"/> (which shapes the reference image).
    /// <c>null</c> uses the author model's default. Supported values depend on
    /// <see cref="SvgModel"/>. Only valid with <see cref="OutputFormatType.Svg"/>.
    /// </summary>
    public SvgReasoningType? SvgReasoning { get; init; }

    /// <summary>File extension of the generated asset for the selected <see cref="OutputFormat"/>.</summary>
    internal string FileExtension => OutputFormat switch
    {
        OutputFormatType.Jpeg => "jpeg",
        OutputFormatType.Webp => "webp",
        OutputFormatType.Svg => "svg",
        OutputFormatType.Gif => "gif",
        _ => "png",
    };

    /// <summary>Complexity levels for GPT Image 2.6.</summary>
    public enum ComplexityType
    {
        /// <summary>Simple compositions, fewer elements.</summary>
        [EnumValue("low")]
        Low,

        /// <summary>Balanced detail.</summary>
        [EnumValue("medium")]
        Medium,

        /// <summary>Rich detail and structure. The server default.</summary>
        [EnumValue("high")]
        High,
    }

    /// <summary>Reasoning effort of the SVG author model.</summary>
    public enum SvgReasoningType
    {
        /// <summary>No reasoning.</summary>
        [EnumValue("none")]
        None,

        /// <summary>Minimal reasoning.</summary>
        [EnumValue("minimal")]
        Minimal,

        /// <summary>Low effort.</summary>
        [EnumValue("low")]
        Low,

        /// <summary>Medium effort.</summary>
        [EnumValue("medium")]
        Medium,

        /// <summary>High effort.</summary>
        [EnumValue("high")]
        High,

        /// <summary>Extra-high effort.</summary>
        [EnumValue("xhigh")]
        XHigh,

        /// <summary>Maximum effort.</summary>
        [EnumValue("max")]
        Max,
    }

    /// <summary>Output formats for GPT Image 2.6.</summary>
    public enum OutputFormatType
    {
        /// <summary>PNG — the default; supports a transparent background.</summary>
        [EnumValue("png")]
        Png,

        /// <summary>JPEG.</summary>
        [EnumValue("jpeg")]
        Jpeg,

        /// <summary>WebP; supports a transparent background.</summary>
        [EnumValue("webp")]
        Webp,

        /// <summary>
        /// SVG — static vector content (no scripts, external resources or embedded
        /// rasters). Generation endpoint only, one image per request.
        /// </summary>
        [EnumValue("svg")]
        Svg,

        /// <summary>Animated GIF — see <see cref="FrameCount"/> and <see cref="FrameDurationMs"/>.</summary>
        [EnumValue("gif")]
        Gif,
    }
}

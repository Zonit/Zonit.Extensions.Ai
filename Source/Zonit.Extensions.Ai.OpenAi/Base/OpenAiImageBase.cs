namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Base class for OpenAI image generation models.
/// </summary>
public abstract class OpenAiImageBase : OpenAiBase, IImageLlm
{
    /// <inheritdoc />
    public virtual ImageQuality Quality { get; init; } = ImageQuality.Standard;

    /// <inheritdoc />
    public virtual ImageSize Size { get; init; } = ImageSize.Square;

    /// <summary>
    /// Gets the size value for API request.
    /// </summary>
    internal string SizeValue => Size switch
    {
        ImageSize.Square => "1024x1024",
        ImageSize.Portrait => "1024x1792",
        ImageSize.Landscape => "1792x1024",
        ImageSize.Small => "512x512",
        ImageSize.Large => "1536x1536",
        _ => "1024x1024"
    };

    /// <summary>
    /// Gets the quality value for API request.
    /// </summary>
    internal string QualityValue => Quality switch
    {
        ImageQuality.Standard => "standard",
        ImageQuality.High => "hd",
        ImageQuality.Ultra => "hd",
        _ => "standard"
    };

    #region Legacy nested types for backward compatibility

    /// <summary>
    /// Legacy quality type for backward compatibility.
    /// Use <see cref="ImageQuality"/> instead.
    /// </summary>
    [Obsolete("Use ImageQuality enum instead.")]
    public enum QualityType
    {
        /// <summary>Auto quality.</summary>
        Auto = 0,
        /// <summary>Low quality.</summary>
        Low = 1,
        /// <summary>Medium quality (same as Standard).</summary>
        Medium = 2,
        /// <summary>High quality.</summary>
        High = 3,
    }

    /// <summary>
    /// Legacy size type for backward compatibility.
    /// Use <see cref="ImageSize"/> instead.
    /// </summary>
    [Obsolete("Use ImageSize enum instead.")]
    public enum SizeType
    {
        /// <summary>Auto size.</summary>
        Auto = 0,
        /// <summary>Square format (1024x1024).</summary>
        Square = 1,
        /// <summary>Landscape format (1792x1024).</summary>
        Landscape = 2,
        /// <summary>Portrait format (1024x1792).</summary>
        Portrait = 3,
    }

    #endregion
}

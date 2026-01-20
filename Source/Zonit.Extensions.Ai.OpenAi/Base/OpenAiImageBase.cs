namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Base class for OpenAI image generation models.
/// </summary>
public abstract class OpenAiImageBase : OpenAiBase, IImageLlm
{
    private ImageQuality _quality = ImageQuality.Standard;
    private ImageSize _size = ImageSize.Square;

    /// <summary>
    /// Image quality setting using <see cref="QualityType"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// new GPTImage1 { Quality = OpenAiImageBase.QualityType.High }
    /// </code>
    /// </example>
    public virtual QualityType Quality
    {
        get => (QualityType)_quality;
        init => _quality = (ImageQuality)value;
    }

    /// <summary>
    /// Image size/dimensions using <see cref="SizeType"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// new GPTImage1 { Size = OpenAiImageBase.SizeType.Landscape }
    /// </code>
    /// </example>
    public virtual SizeType Size
    {
        get => (SizeType)_size;
        init => _size = (ImageSize)value;
    }

    #region IImageLlm implementation

    /// <summary>
    /// Internal: Gets quality for provider implementation.
    /// </summary>
    ImageQuality IImageLlm.Quality => _quality;

    /// <summary>
    /// Internal: Gets size for provider implementation.
    /// </summary>
    ImageSize IImageLlm.Size => _size;

    #endregion

    /// <summary>
    /// Gets the size value for API request.
    /// </summary>
    internal string SizeValue => _size switch
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
    internal string QualityValue => _quality switch
    {
        ImageQuality.Standard => "standard",
        ImageQuality.High => "hd",
        ImageQuality.Ultra => "hd",
        _ => "standard"
    };

    #region Nested types for model configuration

    /// <summary>
    /// Image quality setting for OpenAI image models.
    /// </summary>
    public enum QualityType
    {
        /// <summary>Standard quality, faster generation.</summary>
        Standard = 0,
        /// <summary>High quality, more detailed.</summary>
        High = 1,
        /// <summary>Highest quality, best details.</summary>
        Ultra = 2,
    }

    /// <summary>
    /// Image size/dimensions for OpenAI image models.
    /// </summary>
    public enum SizeType
    {
        /// <summary>Square format (1024x1024).</summary>
        Square = 0,
        /// <summary>Portrait format (1024x1792).</summary>
        Portrait = 1,
        /// <summary>Landscape format (1792x1024).</summary>
        Landscape = 2,
        /// <summary>Small format (512x512).</summary>
        Small = 3,
        /// <summary>Large format (1536x1536).</summary>
        Large = 4,
    }

    #endregion
}

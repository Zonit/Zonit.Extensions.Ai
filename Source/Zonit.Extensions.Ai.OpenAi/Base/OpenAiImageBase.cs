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
}

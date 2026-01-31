namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Base class for X/Grok video generation models.
/// Each derived model defines its own QualityType enum with [EnumValue] attributes.
/// </summary>
/// <typeparam name="TQuality">The model-specific quality enum type.</typeparam>
public abstract class XVideoBase<TQuality> : XBase, IVideoLlm
    where TQuality : struct, Enum
{
    /// <summary>
    /// Video quality setting using model-specific enum.
    /// </summary>
    public abstract TQuality Quality { get; init; }

    /// <summary>
    /// Gets the quality value for API request (from EnumValue attribute).
    /// </summary>
    public string QualityValue => Quality.GetEnumValue();

    /// <summary>
    /// Gets the aspect ratio value for API request (e.g., "16:9", "4:3").
    /// </summary>
    public abstract string AspectRatioValue { get; }

    /// <summary>
    /// Gets the duration in seconds for video generation.
    /// </summary>
    public abstract int DurationSeconds { get; init; }

    /// <summary>
    /// Calculates the price for generating a single video based on quality and duration.
    /// Override in derived classes for specific pricing.
    /// </summary>
    /// <returns>Price in dollars for generating one video.</returns>
    public abstract decimal GetVideoGenerationPrice();
}

namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that supports video generation.
/// Each model defines its own Quality, Size, and Duration parameters.
/// </summary>
public interface IVideoLlm : ILlm
{
    /// <summary>
    /// Gets the quality value for API request (from EnumValue attribute).
    /// </summary>
    string QualityValue { get; }

    /// <summary>
    /// Gets the aspect ratio value for API request (e.g., "16:9", "4:3").
    /// </summary>
    string AspectRatioValue { get; }
    
    /// <summary>
    /// Gets the duration value in seconds for video generation.
    /// </summary>
    int DurationSeconds { get; }
    
    /// <summary>
    /// Calculates the price for generating a single video based on quality and duration.
    /// </summary>
    /// <returns>Price in dollars for generating one video.</returns>
    decimal GetVideoGenerationPrice();
}

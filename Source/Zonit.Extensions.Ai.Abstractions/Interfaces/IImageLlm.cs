namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that supports image generation.
/// Each model defines its own Quality and Size enums with EnumValue attributes.
/// </summary>
public interface IImageLlm : ILlm
{
    /// <summary>
    /// Gets the quality value for API request (from EnumValue attribute).
    /// Not all providers support quality parameter.
    /// </summary>
    string QualityValue { get; }
    
    /// <summary>
    /// Gets the size value for API request (from EnumValue attribute).
    /// </summary>
    string SizeValue { get; }

    /// <summary>
    /// Gets the aspect ratio value for API request (e.g., "16:9", "4:3", "1:1").
    /// Returns null if aspect ratio is not specified (uses provider default).
    /// </summary>
    string? AspectRatioValue => null;
    
    /// <summary>
    /// Calculates the price for generating a single image based on quality and size.
    /// </summary>
    /// <returns>Price in dollars for generating one image.</returns>
    decimal GetImageGenerationPrice();
}

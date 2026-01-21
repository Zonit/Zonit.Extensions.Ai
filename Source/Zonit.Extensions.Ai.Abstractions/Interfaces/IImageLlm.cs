namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that supports image generation.
/// Each model defines its own Quality and Size enums with EnumValue attributes.
/// </summary>
public interface IImageLlm : ILlm
{
    /// <summary>
    /// Gets the quality value for API request (from EnumValue attribute).
    /// </summary>
    string QualityValue { get; }
    
    /// <summary>
    /// Gets the size value for API request (from EnumValue attribute).
    /// </summary>
    string SizeValue { get; }
    
    /// <summary>
    /// Calculates the price for generating a single image based on quality and size.
    /// </summary>
    /// <returns>Price in dollars for generating one image.</returns>
    decimal GetImageGenerationPrice();
}

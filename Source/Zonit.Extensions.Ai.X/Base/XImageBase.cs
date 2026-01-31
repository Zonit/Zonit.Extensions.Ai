namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Base class for X/Grok image generation models with model-specific Quality and Size enums.
/// Each derived model defines its own QualityType and SizeType enums with [EnumValue] attributes.
/// </summary>
/// <typeparam name="TQuality">The model-specific quality enum type.</typeparam>
/// <typeparam name="TSize">The model-specific size enum type.</typeparam>
public abstract class XImageBase<TQuality, TSize> : XBase, IImageLlm
    where TQuality : struct, Enum
    where TSize : struct, Enum
{
    /// <summary>
    /// Image quality setting using model-specific enum.
    /// </summary>
    public abstract TQuality Quality { get; init; }

    /// <summary>
    /// Image size/dimensions using model-specific enum.
    /// </summary>
    public abstract TSize Size { get; init; }

    /// <summary>
    /// Gets the quality value for API request (from EnumValue attribute).
    /// </summary>
    public string QualityValue => Quality.GetEnumValue();

    /// <summary>
    /// Gets the size value for API request (from EnumValue attribute).
    /// </summary>
    public string SizeValue => Size.GetEnumValue();

    /// <summary>
    /// Calculates the price for generating a single image based on quality and size.
    /// Override in derived classes for specific pricing.
    /// </summary>
    /// <returns>Price in dollars for generating one image.</returns>
    public abstract decimal GetImageGenerationPrice();
}

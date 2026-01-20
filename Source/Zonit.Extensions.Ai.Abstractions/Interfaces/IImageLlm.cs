namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that supports image generation.
/// </summary>
public interface IImageLlm : ILlm
{
    /// <summary>
    /// Image quality setting.
    /// </summary>
    ImageQuality Quality { get; }
    
    /// <summary>
    /// Image size/dimensions.
    /// </summary>
    ImageSize Size { get; }
}

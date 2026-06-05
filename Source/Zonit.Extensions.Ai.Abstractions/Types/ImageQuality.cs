namespace Zonit.Extensions.Ai;

/// <summary>
/// Global image generation quality enum.
/// Deprecated: Use model-specific QualityType enums instead (e.g., GPTImage1.QualityType).
/// </summary>
[Obsolete("Use model-specific QualityType enums instead (e.g., GPTImage1.QualityType). Each model defines its own quality options with correct API values.")]
public enum ImageQuality
{
    /// <summary>
    /// Low quality, fastest generation. Maps to "low".
    /// </summary>
    Low = 0,

    /// <summary>
    /// Standard/medium quality. Maps to "medium".
    /// </summary>
    Standard = 1,

    /// <summary>
    /// High quality, most detailed. Maps to "high".
    /// </summary>
    High = 2,

    /// <summary>
    /// Alias for High - GPT Image API only supports low/medium/high.
    /// </summary>
    [Obsolete("GPT Image API only supports low/medium/high. Use High instead.")]
    Ultra = 2
}

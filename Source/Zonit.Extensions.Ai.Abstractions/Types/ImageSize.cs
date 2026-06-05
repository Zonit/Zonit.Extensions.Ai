namespace Zonit.Extensions.Ai;

/// <summary>
/// Global image size/dimensions enum.
/// Deprecated: Use model-specific SizeType enums instead (e.g., GPTImage1.SizeType).
/// </summary>
[Obsolete("Use model-specific SizeType enums instead (e.g., GPTImage1.SizeType). Each model defines its own size options with correct API values.")]
public enum ImageSize
{
    /// <summary>
    /// Square format (1024x1024).
    /// </summary>
    Square,

    /// <summary>
    /// Portrait format (1024x1536).
    /// </summary>
    Portrait,

    /// <summary>
    /// Landscape format (1536x1024).
    /// </summary>
    Landscape,

    /// <summary>
    /// Small square - not supported by GPT Image models.
    /// </summary>
    [Obsolete("GPT Image models don't support 512x512.")]
    Small,

    /// <summary>
    /// Large square - not supported by GPT Image models.
    /// </summary>
    [Obsolete("GPT Image models don't support 1536x1536.")]
    Large
}

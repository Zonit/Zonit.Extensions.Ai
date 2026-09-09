namespace Zonit.Extensions.Ai;

/// <summary>
/// Token breakdown reported by an image endpoint that bills per token rather
/// than per image (see <see cref="ITokenPricedImageLlm"/>). Text and image
/// tokens carry different rates on both sides of the request, so they cannot be
/// collapsed into the flat <see cref="TokenUsage"/> shape used for chat.
/// </summary>
/// <remarks>
/// Mirrors the OpenAI images <c>usage</c> object: <c>input_tokens</c> split by
/// <c>input_tokens_details.text_tokens</c> / <c>image_tokens</c>, and
/// <c>output_tokens</c> split by <c>output_tokens_details</c>. Following the
/// OpenAI convention the cached counts are a SUBSET of their respective input
/// counts, not a separate bucket.
/// </remarks>
public sealed class ImageTokenUsage
{
    /// <summary>Text prompt tokens, inclusive of <see cref="CachedTextInputTokens"/>.</summary>
    public int TextInputTokens { get; init; }

    /// <summary>Text prompt tokens served from cache (subset of <see cref="TextInputTokens"/>).</summary>
    public int CachedTextInputTokens { get; init; }

    /// <summary>Reference/mask image tokens on input, inclusive of <see cref="CachedImageInputTokens"/>.</summary>
    public int ImageInputTokens { get; init; }

    /// <summary>Input image tokens served from cache (subset of <see cref="ImageInputTokens"/>).</summary>
    public int CachedImageInputTokens { get; init; }

    /// <summary>Generated image tokens — the dominant cost of a generation.</summary>
    public int ImageOutputTokens { get; init; }

    /// <summary>Total input tokens (text + image).</summary>
    public int InputTokens => TextInputTokens + ImageInputTokens;

    /// <summary>Total cached input tokens (text + image).</summary>
    public int CachedTokens => CachedTextInputTokens + CachedImageInputTokens;
}

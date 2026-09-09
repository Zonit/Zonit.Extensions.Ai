namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Image 2.5 Flare — the fast member of the GPT Image 2.5 family, tuned for
/// high-quality everyday image generation. For edits that need maximum
/// precision use <see cref="GPTImage25Sunburst"/>.
/// </summary>
/// <remarks>
/// Model id <c>gpt-image-2.5-flare</c> (default snapshot
/// <c>gpt-image-2.5-flare-2026-09-08</c>). Billed per token — see
/// <see cref="GPTImage25Base"/> for the rate card and the size constraints.
/// </remarks>
public class GPTImage25Flare : GPTImage25Base
{
    /// <inheritdoc />
    public override string Name => "gpt-image-2.5-flare";
}

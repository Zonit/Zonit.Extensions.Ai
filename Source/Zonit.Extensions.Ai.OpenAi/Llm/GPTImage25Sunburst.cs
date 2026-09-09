namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Image 2.5 Sunburst — the editing-precision member of the GPT Image 2.5
/// family. Generates and edits images from text and image inputs, and is the
/// one to reach for when an edit has to land exactly where you asked.
/// For fast everyday generation use <see cref="GPTImage25Flare"/>.
/// </summary>
/// <remarks>
/// Model id <c>gpt-image-2.5-sunburst</c> (default snapshot
/// <c>gpt-image-2.5-sunburst-2026-09-08</c>). Billed per token — see
/// <see cref="GPTImage25Base"/> for the rate card and the size constraints.
/// </remarks>
public class GPTImage25Sunburst : GPTImage25Base
{
    /// <inheritdoc />
    public override string Name => "gpt-image-2.5-sunburst";
}

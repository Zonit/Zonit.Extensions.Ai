namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Image 2.6 Flare — the fast member of the GPT Image 2.6 family, generating
/// images in as little as about six seconds. Successor to
/// <see cref="GPTImage25Flare"/> at the same token rates. For edits that need
/// maximum precision use <see cref="GPTImage26Sunburst"/>.
/// </summary>
/// <remarks>
/// Model id <c>gpt-image-2.6-flare</c>, released 4 November 2026. Billed per
/// token — see <see cref="GPTImage26Base"/> for the rate card, the size constraints
/// and the 2.6 options (complexity, SVG and GIF output).
/// </remarks>
public class GPTImage26Flare : GPTImage26Base
{
    /// <inheritdoc />
    public override string Name => "gpt-image-2.6-flare";
}

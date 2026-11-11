namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Image 2.6 Sunburst — the quality and editing-precision member of the GPT
/// Image 2.6 family: higher fidelity, edit consistency and SVG export. Successor to
/// <see cref="GPTImage25Sunburst"/> at the same token rates. For fast everyday
/// generation use <see cref="GPTImage26Flare"/>.
/// </summary>
/// <remarks>
/// Model id <c>gpt-image-2.6-sunburst</c>, released 4 November 2026. Billed per
/// token — see <see cref="GPTImage26Base"/> for the rate card, the size constraints
/// and the 2.6 options (complexity, SVG and GIF output).
/// </remarks>
public class GPTImage26Sunburst : GPTImage26Base
{
    /// <inheritdoc />
    public override string Name => "gpt-image-2.6-sunburst";
}

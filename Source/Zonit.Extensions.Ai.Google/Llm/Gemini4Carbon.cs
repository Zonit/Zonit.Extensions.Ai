namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 4 Carbon — Google's most capable reasoning model (GA 10 November 2026),
/// for agentic coding, multimodal knowledge work, scientific reasoning and
/// cybersecurity defense. More deliberate than <see cref="Gemini4Argon"/>.
/// </summary>
/// <remarks>
/// Model id <c>gemini-4-carbon</c>. Limits, thinking levels and the date-aware rate
/// card ($2 / $10 per 1M through 30 November 2026, $4 / $20 from 1 December) are
/// shared with Argon — see <see cref="Gemini4Base"/>.
/// </remarks>
public class Gemini4Carbon : Gemini4Base
{
    /// <inheritdoc />
    public override string Name => "gemini-4-carbon";
}

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Gemini 4 Argon — Google's efficient Gemini 4 model (GA 13 October 2026), for
/// complex coding, agentic, math and science tasks. Faster than
/// <see cref="Gemini4Carbon"/> and uses fewer tokens per turn, at the same price.
/// </summary>
/// <remarks>
/// Model id <c>gemini-4-argon</c>. Limits, thinking levels and the date-aware rate
/// card ($2 / $10 per 1M through 30 November 2026, $4 / $20 from 1 December) are
/// shared with Carbon — see <see cref="Gemini4Base"/>.
/// </remarks>
public class Gemini4Argon : Gemini4Base
{
    /// <inheritdoc />
    public override string Name => "gemini-4-argon";
}

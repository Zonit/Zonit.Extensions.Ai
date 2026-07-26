namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven v3 — the most expressive model, with the widest language coverage (70+ languages)
/// and support for audio tags / emotional direction. Best for dramatic or emotive delivery.
/// </summary>
public sealed class ElevenV3 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_v3";

    /// <summary>
    /// v3 accepts far less text per request than v2/Turbo/Flash. ElevenLabs' help center cites
    /// roughly 3 000; we use a slightly higher 5 000 (v3 is alpha and the limit has moved) — lower
    /// it if you hit API 400s on long inputs.
    /// </summary>
    public override int MaxCharacters => 5_000;

    /// <summary>v3 is the expressive model — inline audio tags (<c>[excited]</c>, <c>[whispers]</c>…) work here.</summary>
    public override bool SupportsAudioTags => true;

    /// <summary>v3 does <b>not</b> support request stitching — <c>previous_text</c>/<c>next_text</c> are ignored.</summary>
    public override bool SupportsRequestStitching => false;
}

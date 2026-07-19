namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven v3 — the most expressive model, with the widest language coverage (70+ languages)
/// and support for audio tags / emotional direction. Best for dramatic or emotive delivery.
/// </summary>
public sealed class ElevenV3 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_v3";

    /// <inheritdoc />
    public override int MaxCharacters => 10_000;
}

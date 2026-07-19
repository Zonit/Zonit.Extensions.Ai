namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven Flash v2.5 — ultra-low latency (~75 ms) TTS across 32 languages. Built for real-time
/// and conversational use where responsiveness beats maximum fidelity. Billed at ~half the
/// per-character rate of the standard models.
/// </summary>
public sealed class ElevenFlashV2_5 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_flash_v2_5";

    /// <inheritdoc />
    public override int MaxCharacters => 40_000;
}

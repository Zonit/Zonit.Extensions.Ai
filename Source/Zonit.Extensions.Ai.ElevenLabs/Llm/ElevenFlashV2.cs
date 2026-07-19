namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven Flash v2 — ultra-low latency English-only TTS. Use when you need the fastest
/// response and only need English.
/// </summary>
public sealed class ElevenFlashV2 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_flash_v2";

    /// <inheritdoc />
    public override int MaxCharacters => 30_000;
}

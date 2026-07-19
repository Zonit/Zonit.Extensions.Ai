namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven Multilingual v2 — high-quality, stable TTS across 29 languages.
/// A strong default for narration and voice-overs where quality matters more than latency.
/// </summary>
public sealed class ElevenMultilingualV2 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_multilingual_v2";

    /// <inheritdoc />
    public override int MaxCharacters => 10_000;
}

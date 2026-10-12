namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven Turbo v2 — quality/latency balance, English-only. The English counterpart of
/// <see cref="ElevenTurboV2_5"/>.
/// </summary>
[Obsolete("ElevenLabs has deprecated eleven_turbo_v2 in favour of ElevenFlashV2 (eleven_flash_v2), which is faster and cheaper per character. Still functional for now.")]
public sealed class ElevenTurboV2 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_turbo_v2";

    /// <inheritdoc />
    public override int MaxCharacters => 30_000;
}

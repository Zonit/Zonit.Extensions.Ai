namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven Turbo v2 — quality/latency balance, English-only. The English counterpart of
/// <see cref="ElevenTurboV2_5"/>.
/// </summary>
public sealed class ElevenTurboV2 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_turbo_v2";

    /// <inheritdoc />
    public override int MaxCharacters => 30_000;
}

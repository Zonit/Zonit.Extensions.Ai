namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven Turbo v2.5 — balances quality and latency across 32 languages. A good middle ground
/// between <see cref="ElevenMultilingualV2"/> (quality) and <see cref="ElevenFlashV2_5"/> (speed).
/// </summary>
public sealed class ElevenTurboV2_5 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_turbo_v2_5";

    /// <inheritdoc />
    public override int MaxCharacters => 40_000;
}

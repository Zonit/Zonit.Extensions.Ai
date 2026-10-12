namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Eleven v4 — ElevenLabs' flagship text-to-speech model: the most natural and expressive voice,
/// with the widest language coverage. Successor to <see cref="ElevenV3"/>.
/// </summary>
/// <remarks>
/// <para>
/// Released 28 September 2026; ElevenLabs' current generation (v3 is now "previous generation").
/// 90+ languages. Audio tags (<c>[excited]</c>, <c>[whispers]</c>…) and IPA work.
/// </para>
/// <para>
/// Only stability and similarity apply: <see cref="ElevenLabsSpeechBase.Style"/> and
/// <see cref="ElevenLabsSpeechBase.Speed"/> are not available on v4 and are left out of the
/// request. Request stitching is off here (<c>previous_text</c> / <c>next_text</c> are not sent)
/// until ElevenLabs documents v4 support for it.
/// </para>
/// <para>
/// API list price $0.08 per 1K characters (72% launch discount ended 12 October 2026). As with
/// the other ElevenLabs models, <see cref="ElevenLabsSpeechBase.PricePerThousandCharacters"/>
/// stays 0 — the rate depends on your plan; override it to get costs in the metadata.
/// </para>
/// </remarks>
public sealed class ElevenV4 : ElevenLabsSpeechBase
{
    /// <inheritdoc />
    public override string Name => "eleven_v4";

    /// <summary>10 000 characters per request.</summary>
    public override int MaxCharacters => 10_000;

    /// <summary>v4 interprets inline audio tags.</summary>
    public override bool SupportsAudioTags => true;

    /// <summary>Not documented for v4 — <c>previous_text</c>/<c>next_text</c> are not sent.</summary>
    public override bool SupportsRequestStitching => false;

    /// <summary>v4 takes only stability and similarity.</summary>
    public override bool SupportsStyleAndSpeed => false;
}

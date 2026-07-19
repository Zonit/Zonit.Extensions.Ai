namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Voice ids for ElevenLabs' built-in "premade" voices, for IDE-discoverable defaults.
/// </summary>
/// <remarks>
/// A voice is just a string id, so this is a convenience catalog — not an exhaustive or
/// closed set. Any voice id works, including ones you cloned or designed in your ElevenLabs
/// account: pass the raw id string to <c>Voice</c>. Ids are stable ElevenLabs identifiers.
/// </remarks>
public static class ElevenVoices
{
    /// <summary>Rachel — calm, narration (American English).</summary>
    public const string Rachel = "21m00Tcm4TlvDq8ikWAM";

    /// <summary>Adam — deep, narration (American English).</summary>
    public const string Adam = "pNInz6obpgDQGcFmaJgB";

    /// <summary>Antoni — well-rounded (American English).</summary>
    public const string Antoni = "ErXwobaYiN019PkySvjV";

    /// <summary>Bella — soft (American English).</summary>
    public const string Bella = "EXAVITQu4vr4xnSDxMaL";

    /// <summary>Domi — strong (American English).</summary>
    public const string Domi = "AZnzlk1XvdvUeBnXmlld";

    /// <summary>Elli — emotional (American English).</summary>
    public const string Elli = "MF3mGyEYCl7XYWbV9V6O";

    /// <summary>Josh — deep (American English).</summary>
    public const string Josh = "TxGEqnHWrfWFTfGW9XjX";

    /// <summary>Sam — raspy (American English).</summary>
    public const string Sam = "yoZ06aMxZJJ28mfd3POQ";
}

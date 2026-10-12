namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Voice ids for xAI's built-in text-to-speech voices, for IDE-discoverable defaults.
/// </summary>
/// <remarks>
/// A voice is just a string id, so this is a convenience catalog — not a closed set. Ids are
/// case-insensitive; the full list is at <c>GET https://api.x.ai/v1/tts/voices</c>, and custom
/// voices created through <c>/v1/custom-voices</c> work too: pass the raw id to
/// <see cref="GrokTts.Voice"/>.
/// </remarks>
public static class GrokVoices
{
    /// <summary>Eve — the API default.</summary>
    public const string Eve = "eve";

    /// <summary>Ara.</summary>
    public const string Ara = "ara";

    /// <summary>Rex.</summary>
    public const string Rex = "rex";
}

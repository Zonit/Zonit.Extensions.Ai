namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Controls ElevenLabs text normalization (<c>apply_text_normalization</c>) — how numbers,
/// dates, currencies and abbreviations are spelled out before synthesis.
/// </summary>
public enum TextNormalizationMode
{
    /// <summary>Let ElevenLabs decide (the API default).</summary>
    [EnumValue("auto")]
    Auto,

    /// <summary>Always normalize. Not supported by Flash/Turbo v2.5 (may be rejected there).</summary>
    [EnumValue("on")]
    On,

    /// <summary>Never normalize — speak the text exactly as written.</summary>
    [EnumValue("off")]
    Off,
}

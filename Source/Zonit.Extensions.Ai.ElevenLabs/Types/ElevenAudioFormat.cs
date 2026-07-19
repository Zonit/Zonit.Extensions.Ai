using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Output audio format for ElevenLabs text-to-speech. The <see cref="EnumValueAttribute"/>
/// on each member is the exact <c>output_format</c> value sent to the API.
/// </summary>
/// <remarks>
/// Naming: <c>{codec}_{sampleRateHz}_{bitrateKbps}</c>. Higher MP3 bitrates and PCM sample
/// rates may require a paid ElevenLabs tier — the API rejects a format the account can't use.
/// </remarks>
public enum ElevenAudioFormat
{
    /// <summary>MP3, 22.05 kHz, 32 kbps.</summary>
    [EnumValue("mp3_22050_32")] Mp3_22050_32,

    /// <summary>MP3, 44.1 kHz, 32 kbps.</summary>
    [EnumValue("mp3_44100_32")] Mp3_44100_32,

    /// <summary>MP3, 44.1 kHz, 64 kbps.</summary>
    [EnumValue("mp3_44100_64")] Mp3_44100_64,

    /// <summary>MP3, 44.1 kHz, 96 kbps.</summary>
    [EnumValue("mp3_44100_96")] Mp3_44100_96,

    /// <summary>MP3, 44.1 kHz, 128 kbps. The default — good quality, broad compatibility.</summary>
    [EnumValue("mp3_44100_128")] Mp3_44100_128,

    /// <summary>MP3, 44.1 kHz, 192 kbps (requires a paid tier).</summary>
    [EnumValue("mp3_44100_192")] Mp3_44100_192,

    /// <summary>Raw PCM (16-bit little-endian), 16 kHz.</summary>
    [EnumValue("pcm_16000")] Pcm_16000,

    /// <summary>Raw PCM (16-bit little-endian), 22.05 kHz.</summary>
    [EnumValue("pcm_22050")] Pcm_22050,

    /// <summary>Raw PCM (16-bit little-endian), 24 kHz.</summary>
    [EnumValue("pcm_24000")] Pcm_24000,

    /// <summary>Raw PCM (16-bit little-endian), 44.1 kHz.</summary>
    [EnumValue("pcm_44100")] Pcm_44100,

    /// <summary>μ-law, 8 kHz — for telephony (e.g. Twilio).</summary>
    [EnumValue("ulaw_8000")] Ulaw_8000,
}

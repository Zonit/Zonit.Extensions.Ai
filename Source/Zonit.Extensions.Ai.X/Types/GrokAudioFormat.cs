namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Output audio format for xAI text-to-speech (<see cref="GrokTts"/>). The
/// <see cref="EnumValueAttribute"/> encodes <c>{codec}_{sampleRateHz}[_{bitrateBps}]</c>; the
/// provider splits it into the request's <c>output_format</c> object.
/// </summary>
public enum GrokAudioFormat
{
    /// <summary>MP3, 24 kHz, 128 kbps. The API default.</summary>
    [EnumValue("mp3_24000_128000")] Mp3_24000_128,

    /// <summary>MP3, 44.1 kHz, 128 kbps.</summary>
    [EnumValue("mp3_44100_128000")] Mp3_44100_128,

    /// <summary>MP3, 44.1 kHz, 192 kbps.</summary>
    [EnumValue("mp3_44100_192000")] Mp3_44100_192,

    /// <summary>MP3, 48 kHz, 192 kbps.</summary>
    [EnumValue("mp3_48000_192000")] Mp3_48000_192,

    /// <summary>WAV, 24 kHz.</summary>
    [EnumValue("wav_24000")] Wav_24000,

    /// <summary>WAV, 44.1 kHz.</summary>
    [EnumValue("wav_44100")] Wav_44100,

    /// <summary>WAV, 48 kHz.</summary>
    [EnumValue("wav_48000")] Wav_48000,

    /// <summary>Raw PCM (16-bit little-endian), 16 kHz.</summary>
    [EnumValue("pcm_16000")] Pcm_16000,

    /// <summary>Raw PCM (16-bit little-endian), 24 kHz.</summary>
    [EnumValue("pcm_24000")] Pcm_24000,

    /// <summary>Raw PCM (16-bit little-endian), 48 kHz.</summary>
    [EnumValue("pcm_48000")] Pcm_48000,

    /// <summary>µ-law, 8 kHz — telephony.</summary>
    [EnumValue("mulaw_8000")] Mulaw_8000,

    /// <summary>A-law, 8 kHz — telephony.</summary>
    [EnumValue("alaw_8000")] Alaw_8000,
}

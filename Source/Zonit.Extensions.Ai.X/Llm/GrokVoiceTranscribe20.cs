namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok Voice Transcribe 2.0 — xAI's speech-to-text model (released 18 September 2026),
/// replacing <c>grok-voice-transcribe-1.0</c>, which reached end of life on 2 October 2026.
/// </summary>
/// <remarks>
/// <para>
/// Model id <c>grok-voice-transcribe-2.0</c>, served on <c>POST /v1/stt</c> (multipart upload).
/// Container formats are detected automatically: WAV, MP3, OGG, Opus, FLAC, AAC, MP4, M4A, MKV.
/// Up to 500 MB per file; 38 languages plus automatic detection.
/// </para>
/// <para>
/// $0.10 per hour of audio on the REST endpoint this package uses ($0.20 per hour streaming).
/// Diarization, word timestamps and key terms carry no extra charge.
/// </para>
/// </remarks>
public class GrokVoiceTranscribe20 : XBase, IAudioLlm
{
    /// <inheritdoc />
    public override string Name => "grok-voice-transcribe-2.0";

    /// <inheritdoc />
    public override decimal PriceInput => 0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <summary>Price per hour of audio transcribed (REST).</summary>
    public decimal PricePerHour => 0.10m;

    /// <summary>Price per minute of audio transcribed (REST) — <see cref="PricePerHour"/> / 60.</summary>
    public decimal PricePerMinute => PricePerHour / 60m;

    /// <inheritdoc />
    public override int MaxInputTokens => 0;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Audio;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures => FeaturesType.None;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Transcription;
}

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-4o Transcribe Diarize — speech-to-text that also identifies who is speaking
/// and when (speaker diarization). Returns speaker-segmented transcripts; use when a
/// recording has multiple speakers (interviews, meetings, calls).
/// </summary>
public class GPT4oTranscribeDiarize : OpenAiBase, IAudioLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-4o-transcribe-diarize";

    /// <inheritdoc />
    public override decimal PriceInput => 0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <summary>
    /// Price per minute of audio transcribed.
    /// </summary>
    public decimal PricePerMinute => 0.012m;

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

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Transcribe — OpenAI's current speech-to-text model (released 28 July 2026),
/// replacing <c>whisper-1</c> and the <c>gpt-4o-*-transcribe</c> family, which shut
/// down on 26 February 2027.
/// </summary>
/// <remarks>
/// Model id <c>gpt-transcribe</c>, served on <c>/v1/audio/transcriptions</c>.
/// $0.0045 per minute of audio.
/// </remarks>
public class GPTTranscribe : OpenAiBase, IAudioLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-transcribe";

    /// <inheritdoc />
    public override decimal PriceInput => 0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <summary>
    /// Price per minute of audio transcribed.
    /// </summary>
    public decimal PricePerMinute => 0.0045m;

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

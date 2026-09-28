namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Whisper - General-purpose speech recognition model (legacy).
/// </summary>
[Obsolete("whisper-1 is OpenAI's legacy speech-recognition model and is shut down on 2027-02-26. Use GPTTranscribe (gpt-transcribe).")]
public class Whisper1 : OpenAiBase, IAudioLlm
{
    /// <inheritdoc />
    public override string Name => "whisper-1";

    /// <inheritdoc />
    public override decimal PriceInput => 0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <summary>
    /// Price per minute of audio transcribed.
    /// </summary>
    public decimal PricePerMinute => 0.006m;

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

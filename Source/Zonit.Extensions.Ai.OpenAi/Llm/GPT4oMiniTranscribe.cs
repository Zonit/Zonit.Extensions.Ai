namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-4o mini Transcribe — cost-efficient speech-to-text model powered by GPT-4o mini.
/// A lighter, cheaper alternative to <see cref="GPT4oTranscribe"/> for high-volume transcription.
/// </summary>
public class GPT4oMiniTranscribe : OpenAiBase, IAudioLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-4o-mini-transcribe";

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

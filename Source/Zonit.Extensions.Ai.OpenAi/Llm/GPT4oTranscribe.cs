namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-4o Transcribe - Speech-to-text model powered by GPT-4o.
/// </summary>
[Obsolete("gpt-4o-transcribe is deprecated: OpenAI shuts it down on 2027-02-26 — migrate to GPTTranscribe (gpt-transcribe).")]
public class GPT4oTranscribe : OpenAiBase, IAudioLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-4o-transcribe";

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

using Zonit.Extensions.Ai.ElevenLabs;

namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// Base class for ElevenLabs text-to-speech models. A concrete model sets its
/// <see cref="LlmBase.Name"/> to the ElevenLabs <c>model_id</c> (e.g. <c>eleven_multilingual_v2</c>);
/// the instance also carries the voice, output format and voice settings used for the request.
/// </summary>
/// <remarks>
/// The design mirrors <c>OpenAiImageBase</c>: configuration lives on the model instance, the
/// caller passes only the text. Every knob has a safe default, so
/// <c>new SomeModel()</c> already produces valid audio without further setup.
/// </remarks>
public abstract class ElevenLabsSpeechBase : LlmBase, ISpeechLlm
{
    /// <summary>
    /// Voice id to speak in. Defaults to <see cref="ElevenVoices.Rachel"/>. Accepts any
    /// ElevenLabs voice id — a premade one from <see cref="ElevenVoices"/> or one you cloned.
    /// </summary>
    public string Voice { get; init; } = ElevenVoices.Rachel;

    /// <summary>
    /// Output audio format. Defaults to <see cref="ElevenAudioFormat.Mp3_44100_128"/>.
    /// </summary>
    public ElevenAudioFormat Format { get; init; } = ElevenAudioFormat.Mp3_44100_128;

    /// <summary>
    /// Voice stability (0..1). Lower is more expressive/variable, higher is more stable/monotone.
    /// Default: 0.5.
    /// </summary>
    public double Stability { get; init; } = 0.5;

    /// <summary>
    /// Similarity boost (0..1) — how closely the output adheres to the original voice.
    /// Default: 0.75.
    /// </summary>
    public double SimilarityBoost { get; init; } = 0.75;

    /// <summary>
    /// Style exaggeration (0..1). 0 disables it (fastest, most stable). Default: 0.
    /// </summary>
    public double Style { get; init; } = 0.0;

    /// <summary>
    /// Whether to boost similarity to the speaker at a small latency cost. Default: true.
    /// </summary>
    public bool UseSpeakerBoost { get; init; } = true;

    /// <summary>
    /// Maximum number of input characters accepted per request. The provider rejects longer
    /// text up front with a clear error instead of letting the API return an opaque 400.
    /// Default: 10 000. Override per model where the real limit differs.
    /// </summary>
    public virtual int MaxCharacters => 10_000;

    /// <summary>
    /// Price in dollars per 1 000 input characters. Default 0 — override per model with the
    /// rate for your ElevenLabs plan. TTS is billed per character, so token prices are 0.
    /// </summary>
    public virtual decimal PricePerThousandCharacters => 0m;

    /// <inheritdoc />
    public string VoiceValue => Voice;

    /// <inheritdoc />
    public string FormatValue => Format.GetEnumValue();

    /// <inheritdoc />
    public decimal GetSpeechGenerationPrice(int characterCount)
        => characterCount / 1000m * PricePerThousandCharacters;

    // --- Fixed model shape for a TTS endpoint ---

    /// <inheritdoc />
    public override decimal PriceInput => 0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 0;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Audio;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Speech;
}

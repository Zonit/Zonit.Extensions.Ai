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
    /// Speaking speed. 1.0 = natural pace; below 1.0 slows down, above speeds up. ElevenLabs
    /// accepts roughly 0.7–1.2 (most natural around 0.9–1.1). Default: 1.0.
    /// </summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Largest seed value ElevenLabs accepts.</summary>
    public const long MaxSeed = 4_294_967_295;

    /// <summary>
    /// Generation seed (0..<see cref="MaxSeed"/>). When set, ElevenLabs makes a
    /// <i>best-effort</i> to sample deterministically, so the same text with the same seed
    /// and settings returns the same audio (determinism is not guaranteed).
    /// <para>
    /// <b>Default: a random seed generated once per model instance.</b> Because a model object
    /// is normally reused for every line of a script, that one seed is sent on every call, so
    /// the narrator's character stays consistent line to line even if you never touch this
    /// property — combine with <see cref="SpeechPrompt.PreviousText"/> / <see cref="SpeechPrompt.NextText"/>
    /// for intonation continuity. Set your own value for full reproducibility across runs, or
    /// set it to <c>null</c> to opt out and let ElevenLabs pick a fresh (unreturned) seed per call.
    /// </para>
    /// </summary>
    public long? Seed { get; init; } = NewRandomSeed();

    /// <summary>Generates a random seed in the ElevenLabs-accepted range [0, <see cref="MaxSeed"/>].</summary>
    private static long NewRandomSeed() => System.Random.Shared.NextInt64(0, MaxSeed + 1);

    /// <summary>
    /// Optional ISO language code (e.g. <c>"en"</c>, <c>"pl"</c>) to enforce the spoken language.
    /// Some models (Turbo/Flash v2.5) honour it; others infer language from the text. Default:
    /// <c>null</c> (auto-detect).
    /// </summary>
    public string? LanguageCode { get; init; }

    /// <summary>
    /// How numbers, dates, currencies and abbreviations are spelled out before synthesis
    /// (e.g. "$5" → "five dollars"). Default: <see cref="TextNormalizationMode.Auto"/> (the API
    /// decides). Note: Flash/Turbo v2.5 support only Auto/Off — <see cref="TextNormalizationMode.On"/>
    /// may be rejected there.
    /// </summary>
    public TextNormalizationMode TextNormalization { get; init; } = TextNormalizationMode.Auto;

    /// <summary>
    /// Maximum number of input characters accepted per request. The provider rejects longer
    /// text up front with a clear error instead of letting the API return an opaque 400.
    /// Default: 10 000. Override per model where the real limit differs.
    /// </summary>
    public virtual int MaxCharacters => 10_000;

    /// <summary>
    /// Whether inline <b>audio tags</b> (<c>[excited]</c>, <c>[whispers]</c>, …) direct the delivery
    /// on this model. Only <see cref="Zonit.Extensions.Ai.ElevenLabs.ElevenV3"/> interprets them;
    /// other models read the brackets literally. Informational — tags live in the text. Default: false.
    /// </summary>
    public virtual bool SupportsAudioTags => false;

    /// <summary>
    /// Whether this model supports <b>request stitching</b> — the <c>previous_text</c>/<c>next_text</c>
    /// context that keeps prosody continuous across separate calls. True for v2/Turbo/Flash; <b>false
    /// for v3</b>. When false, the provider omits that context (v3 uses audio tags for expression
    /// instead). Default: true.
    /// </summary>
    public virtual bool SupportsRequestStitching => true;

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

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// xAI text-to-speech — the Grok voices on <c>POST /v1/tts</c>. Configuration lives on the
/// instance, the caller passes only the text:
/// <code>
/// var voice = new GrokTts { Voice = GrokVoices.Ara, Language = "pl" };
/// Result&lt;Asset&gt; audio = await ai.GenerateAsync(voice, "Cześć, jak się masz?");
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// $15 per 1M characters of input text (pronunciation substitutions are not billed). Up to
/// 60,000 characters per request. Inline speech tags in the text direct the delivery.
/// </para>
/// <para>
/// The endpoint takes no model id — <see cref="Name"/> (<c>grok-tts</c>) is a label for
/// metadata and cost reports and is never sent. There is no request stitching:
/// <see cref="SpeechPrompt.PreviousText"/> / <see cref="SpeechPrompt.NextText"/> are ignored.
/// Timed synthesis (<c>with_timestamps</c>) is supported.
/// </para>
/// </remarks>
public class GrokTts : XBase, ISpeechLlm
{
    /// <summary>Largest input accepted per request.</summary>
    public const int MaxCharacters = 60_000;

    /// <summary>Lowest accepted <see cref="Speed"/>.</summary>
    public const double MinSpeed = 0.7;

    /// <summary>Highest accepted <see cref="Speed"/>.</summary>
    public const double MaxSpeed = 1.5;

    /// <summary>
    /// Voice id to speak in. Defaults to <see cref="GrokVoices.Eve"/>. Accepts any built-in or
    /// custom xAI voice id.
    /// </summary>
    public string Voice { get; init; } = GrokVoices.Eve;

    /// <summary>
    /// Spoken language as a BCP-47 code (e.g. <c>"en"</c>, <c>"pl"</c>, <c>"pt-BR"</c>), or
    /// <c>"auto"</c> to detect it from the text. The API requires the field. Default: <c>"auto"</c>.
    /// </summary>
    public string Language { get; init; } = "auto";

    /// <summary>Output audio format. Defaults to <see cref="GrokAudioFormat.Mp3_24000_128"/>.</summary>
    public GrokAudioFormat Format { get; init; } = GrokAudioFormat.Mp3_24000_128;

    /// <summary>Speaking speed, <see cref="MinSpeed"/>–<see cref="MaxSpeed"/>. Default: 1.0.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>
    /// Whether numbers, dates and abbreviations are spelled out before synthesis
    /// (<c>text_normalization</c>). Default: <c>false</c> (the API default).
    /// </summary>
    public bool TextNormalization { get; init; }

    /// <summary>Price in dollars per 1 000 input characters ($15 per 1M).</summary>
    public decimal PricePerThousandCharacters => 0.015m;

    /// <inheritdoc />
    public override string Name => "grok-tts";

    /// <inheritdoc />
    public string VoiceValue => Voice;

    /// <inheritdoc />
    public string FormatValue => Format.GetEnumValue();

    /// <inheritdoc />
    public decimal GetSpeechGenerationPrice(int characterCount)
        => characterCount / 1000m * PricePerThousandCharacters;

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
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures => FeaturesType.None;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Speech;
}

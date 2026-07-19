namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that synthesizes speech from text (text-to-speech / TTS).
/// </summary>
/// <remarks>
/// This is the mirror image of <see cref="IAudioLlm"/> (transcription, audio → text):
/// a speech model takes <b>text</b> and produces an <b>audio</b> <see cref="Zonit.Extensions.Asset"/>.
/// <para>
/// Following the same convention as <see cref="IImageLlm"/>, the concrete model instance
/// carries its own configuration — the voice it speaks in (<see cref="VoiceValue"/>) and the
/// output audio format (<see cref="FormatValue"/>) — and the caller supplies only the content
/// (the text) at the call site:
/// <code>
/// var voice = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };
/// Result&lt;Asset&gt; audio = await ai.GenerateAsync(voice, "Cześć, jak się masz?");
/// await File.WriteAllBytesAsync("out.mp3", audio.Value.Data);
/// </code>
/// </para>
/// </remarks>
public interface ISpeechLlm : ILlm
{
    /// <summary>
    /// Identifier of the voice the text is rendered in (the provider's voice id).
    /// Provider-specific and free-form — TTS providers expose thousands of built-in
    /// voices plus user-cloned ones, so this is a string rather than an enum.
    /// </summary>
    string VoiceValue { get; }

    /// <summary>
    /// Output audio format as sent to the API (wire value, e.g. <c>"mp3_44100_128"</c>).
    /// Backed by a model-specific format enum with <see cref="EnumValueAttribute"/> so the
    /// caller picks from a fixed, IDE-discoverable set rather than typing a raw string.
    /// </summary>
    string FormatValue { get; }

    /// <summary>
    /// Calculates the price for synthesizing <paramref name="characterCount"/> characters of
    /// input text. TTS is billed per character, not per token.
    /// </summary>
    /// <param name="characterCount">Number of input characters to synthesize.</param>
    /// <returns>Price in dollars for the synthesis.</returns>
    decimal GetSpeechGenerationPrice(int characterCount);
}

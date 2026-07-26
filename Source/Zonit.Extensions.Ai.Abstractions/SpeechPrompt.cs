namespace Zonit.Extensions.Ai;

/// <summary>
/// A single line to synthesize, plus optional surrounding text so the model can keep
/// intonation continuous across separately-synthesized lines.
/// </summary>
/// <remarks>
/// <para>
/// TTS providers synthesize each request independently, so prosody (pitch, pacing,
/// emphasis) resets at every call — audible as "jumps" when a script is rendered
/// line-by-line or scene-by-scene. <see cref="PreviousText"/> and <see cref="NextText"/>
/// feed the model the lines immediately before and after the current one as
/// <b>context only</b>: they are not spoken and not billed, but they let the narrator
/// flow naturally out of the previous line and lead into the next.
/// </para>
/// <para>
/// Only providers that support it use these (e.g. ElevenLabs' <c>previous_text</c> /
/// <c>next_text</c>); providers without the feature ignore them. The plain-string
/// overload <c>GenerateAsync(ISpeechLlm, string)</c> is equivalent to a
/// <see cref="SpeechPrompt"/> with no surrounding context.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var voice = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };
///
/// var line = await ai.GenerateAsync(voice, new SpeechPrompt
/// {
///     Text         = "I couldn't believe my eyes.",
///     PreviousText = "She slowly opened the ancient door.",  // just spoken
///     NextText     = "The whole room was made of gold.",      // spoken next
/// });
/// </code>
/// </example>
public sealed class SpeechPrompt
{
    /// <summary>The text that is actually spoken (and billed).</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The line spoken immediately before <see cref="Text"/>, used only to keep prosody
    /// continuous. Not spoken, not billed. Leave <c>null</c> for the first line.
    /// </summary>
    public string? PreviousText { get; init; }

    /// <summary>
    /// The line spoken immediately after <see cref="Text"/>, used only to keep prosody
    /// continuous. Not spoken, not billed. Leave <c>null</c> for the last line.
    /// </summary>
    public string? NextText { get; init; }
}

using System.ComponentModel;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Response for translation prompt.
/// </summary>
public class TranslateResponse
{
    /// <summary>
    /// The translated text.
    /// </summary>
    [Description("Translated text in the target language")]
    public required string TranslatedText { get; set; }

    /// <summary>
    /// Detected source language (ISO 639-1 code).
    /// </summary>
    [Description("Detected source language ISO 639-1 code (e.g., 'en', 'pl', 'de')")]
    public string? DetectedLanguage { get; set; }

    /// <summary>
    /// Confidence score (0-1).
    /// </summary>
    [Description("Translation confidence score from 0.0 to 1.0")]
    public double? Confidence { get; set; }
}

/// <summary>
/// Prompt for translating text to a target language.
/// </summary>
/// <example>
/// var result = await ai.GenerateAsync(
///     new GPT51(), 
///     new TranslatePrompt { Content = "Hello world!", Language = "Polish" });
/// Console.WriteLine(result.Value.TranslatedText); // "Witaj świecie!"
/// </example>
public class TranslatePrompt : PromptBase<TranslateResponse>
{
    /// <summary>
    /// Text to translate.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Target language name (e.g., "Polish", "German", "French").
    /// </summary>
    public required string Language { get; init; }

    /// <inheritdoc />
    public override string Prompt => @"
Translate the following text into {{ language }}.
Detect the source language and provide a confidence score.

Text to translate:
{{ content }}
";
}

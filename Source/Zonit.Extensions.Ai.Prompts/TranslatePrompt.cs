using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Translates text into a target language as a native writer would — preserving meaning, structure
/// and non-translatable tokens, while localizing punctuation, numbers, dates and typography to the
/// conventions of the target language.
/// </summary>
/// <remarks>
/// <para>
/// This is a <em>thin localization step</em>: it does not summarize, expand, rewrite or re-order the
/// content — it re-expresses it. Per-language conventions live in Scriban <c>{{ if target_language == "xx" }}</c>
/// branches, so new languages are added by appending a section, without touching the rendering code.
/// Each call is independent (no shared state), so a pipeline may fan out the same source text across
/// many target languages in parallel.
/// </para>
/// <para>
/// Languages with a dedicated section: <c>en, pl, de, es, fr, it, pt, nl, sv, da, no, fi, ru, uk, cs,
/// sk, hu, tr, ar</c>. Any other target falls back to general professional-translation rules.
/// </para>
/// </remarks>
/// <example>
/// // Target accepts any ISO 639-1 / culture code via the Culture value object.
/// var result = await ai.GenerateAsync(
///     new GPT51(),
///     new TranslatePrompt { Content = "Hello world!", Target = "pl" });
/// Console.WriteLine(result.Value); // "Witaj świecie!"
///
/// // Explicit source language (otherwise auto-detected):
/// new TranslatePrompt { Content = text, Source = "en", Target = "de-DE" };
/// </example>
// Returns the translation as a plain string (not a structured object) on purpose:
// the deliverable is just the translated text, and a string response cannot fail
// JSON parsing — the model's free text may legitimately contain quotes, braces and
// other characters that break structured-JSON output on some providers/models.
public class TranslatePrompt : PromptBase<string>
{
    /// <summary>
    /// Text to translate.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Target language as a <see cref="Culture"/> value object (e.g. <c>"pl"</c>, <c>"pl-PL"</c>, <c>"de"</c>).
    /// A string assigned here is converted implicitly.
    /// </summary>
    public required Culture Target { get; init; }

    /// <summary>
    /// Optional source language as a <see cref="Culture"/>. Leave unset (<see cref="Culture.Empty"/>) to auto-detect.
    /// </summary>
    public Culture Source { get; init; }

    /// <summary>Two-letter target code that selects the language section (e.g. <c>"pl"</c>); Norwegian variants map to <c>"no"</c>.</summary>
    public string TargetLanguage => NormalizeCode(Target);

    /// <summary>Two-letter source code, or empty when the source is auto-detected.</summary>
    public string SourceLanguage => NormalizeCode(Source);

    /// <summary>English display name of the target language, for natural phrasing in the prompt (e.g. <c>"Polish"</c>).</summary>
    public string TargetName => Target.EnglishName ?? Target.Value;

    /// <summary>English display name of the source language, or empty when auto-detected.</summary>
    public string SourceName => Source.HasValue ? (Source.EnglishName ?? Source.Value) : string.Empty;

    /// <summary>
    /// Two-letter ISO 639-1 code of a culture, lowercased, with Norwegian Bokmål/Nynorsk collapsed
    /// to <c>"no"</c>. Returns empty for <see cref="Culture.Empty"/>.
    /// </summary>
    private static string NormalizeCode(Culture culture)
    {
        var code = culture.LanguageCode?.ToLowerInvariant();
        if (string.IsNullOrEmpty(code))
            return string.Empty;

        return code is "nb" or "nn" ? "no" : code;
    }

    /// <inheritdoc />
    public override string Prompt => """
# Role

You are a professional {{ target_name }} translator and localization specialist — a native-level writer, not a word-for-word converter. Your task is to re-express the SOURCE TEXT in {{ target_name }} so that a {{ target_name }} reader receives it as if it had been written in {{ target_name }} from the start: the meaning, intent, tone and structure are the author's, but every convention of spelling, punctuation, numbers, dates and typography is the one {{ target_name }} actually uses.

{{ if source_language != "" }}The source text is written in {{ source_name }}.{{ else }}First identify the source language, then translate.{{ end }}

# Preserve exactly — in every language

- **Meaning and completeness.** Convey everything the source says, add nothing, omit nothing. Translate all human-readable text — titles, headings and labels included — and keep only brand and product names in the original. When a phrase has no literal equivalent, render its intent rather than its words.
- **Layout and markup.** Keep the same paragraphs, line breaks, headings, lists, tables and ordering. Reproduce Markdown, HTML/XML tags, attributes and code fences unchanged — translate only the human-readable text between them.
- **Non-translatable tokens, verbatim.** Source code, commands, file paths, URLs, e-mail addresses, hashtags, @handles, emoji, and placeholders / interpolation tokens such as `{0}`, `{{ "{{name}}" }}`, `%s`, `:id`, `$VAR`. Copy them character-for-character and keep them in place.
- **Proper nouns and identifiers.** People, brands, products, organisations, trademarks, model and part numbers, ISO codes and version strings stay as written. For a target language in another script, render well-established names by that language's accepted convention (see its section); identifiers and Latin acronyms stay Latin.
- **Numeric values.** Every quantity, amount, percentage and measurement keeps its exact value. Localize only the *format* — decimal mark, digit grouping, date order — to the convention in the language section below, and keep each number internally consistent in a single convention.
- **Clock times and time zones.** Copy every time of day and its zone label exactly as written, and never convert between zones — `13:30 UTC` stays `13:30 UTC`, `9:00 CEST` stays `9:00 CEST`. Date *format* is localized per the language section; the time and its zone are not.

# Write like a native — in every language

- **Idiomatic, not literal.** Translate the idea. Replace idioms, set phrases and metaphors with their natural {{ target_name }} counterpart instead of carrying the source wording across.
- **Register.** Detect the register of the source (formal, casual, marketing, technical, legal) and reproduce the same level in {{ target_name }}. Where {{ target_name }} forces a choice between a formal and a familiar form of address, follow the source's formality — the language section notes the default.
- **Consistency.** Translate one source term the same way every time it appears, and keep one voice across the whole text.
- **Native typography.** Use {{ target_name }}'s own quotation marks, dash conventions, separators and date format. Apply these as you compose — they are how the language is written, not a pass to run at the end. The section below is authoritative for {{ target_name }}.

# {{ target_name }} conventions ({{ target_language }})

{{ if target_language == "en" }}
- **Quotation marks:** primary "…", nested '…'; use curly typographic quotes in prose.
- **Numbers:** point decimal, comma grouping — `3.14`, `1,234,567`. Percent sign closes onto the number: `12.5%`.
- **Dates:** US English `May 26, 2026` or `5/26/2026`; British English `26 May 2026` or `26/05/2026`. Match the variant requested; default to US usage. Month and weekday names are capitalized.
- **Dash:** the em-dash `—` is natural for an aside and is usually set unspaced in US style; British style prefers a spaced en-dash ` – `.
- **Style:** sentence case in body text; keep the source's heading case. No special diacritics — loanwords keep theirs (`café`, `naïve`).
{{ end }}
{{ if target_language == "pl" }}
- **Quotation marks:** primary „…", nested «…».
- **Numbers:** comma decimal, space grouping — `3,14`, `1 234 567`. Percent sign closes onto the number: `12,5%`.
- **Dates:** numeric `26.05.2026`; long `26 maja 2026` (month in the genitive, lowercase); with weekday `wtorek, 26 maja 2026`.
- **Dash:** Polish prose does not use the em-dash for asides. Write an apposition with a comma (`X, opis`) or parentheses (`X (opis)`), and recast every in-sentence `—` that way as you compose; keep a dash only in a heading label or the sign-off, where it is the hyphen `-`.
- **Alphabet:** keep full diacritics `ą ć ę ł ń ó ś ź ż` everywhere.
- **Address:** formal *Pan / Pani* by default; *ty* only when the source is clearly casual.
- **Style:** prefer natural Polish syntax to anglicised word order; keep loanwords Polish readers genuinely use.
{{ end }}
{{ if target_language == "de" }}
- **Quotation marks:** primary „…", nested ‚…'; the print/Swiss alternative is »…«.
- **Numbers:** comma decimal, point grouping — `3,14`, `1.234.567` (a thin space is also fine).
- **Dates:** numeric `26.05.2026`; long `26. Mai 2026` (day takes a period, month capitalized); with weekday `Montag, 26. Mai 2026`.
- **Dash:** use the spaced en-dash `–` (Gedankenstrich) for an aside, not the em-dash.
- **Alphabet:** keep `ä ö ü ß` (Swiss German replaces `ß` with `ss`); capitalize every noun.
- **Address:** formal *Sie* by default; *du* for clearly casual or youth-facing copy.
- **Style:** German compounds are natural — prefer one well-formed compound to a loose paraphrase.
{{ end }}
{{ if target_language == "es" }}
- **Quotation marks:** primary «…» (RAE), nested "…".
- **Punctuation:** open questions and exclamations with inverted marks — `¿…?`, `¡…!`.
- **Numbers:** comma decimal, point grouping in Spain — `3,14`, `1.234.567`; much of Latin America uses point decimal and comma grouping. Default to Spain unless the target is a specific Latin-American locale.
- **Dates:** `26 de mayo de 2026` (month lowercase) or `26/05/2026`.
- **Dash:** the em-dash (raya) `—` is standard for asides and dialogue.
- **Alphabet:** keep `á é í ó ú ü ñ`.
- **Address:** formal *usted* by default; *tú* when casual. Use *ustedes* for the plural (and *vosotros* only for Spain-informal).
{{ end }}
{{ if target_language == "fr" }}
- **Quotation marks:** « … » with a non-breaking space inside each guillemet; nested "…".
- **Spacing:** a non-breaking space precedes `;` `:` `!` `?` `»` and follows `«`.
- **Numbers:** comma decimal, non-breaking-space grouping — `3,14`, `1 234 567`.
- **Dates:** `26 mai 2026` (month lowercase, no comma); with weekday `lundi 26 mai 2026`.
- **Dash:** the em-dash (tiret cadratin) `—` for asides, set with spaces; en-dash for ranges.
- **Alphabet:** keep the full accent set (`à â ç é è ê ë î ï ô û ù ü ÿ œ æ`); use the typographic apostrophe `’`.
- **Address:** formal *vous* by default; *tu* when casual.
{{ end }}
{{ if target_language == "it" }}
- **Quotation marks:** primary «…» (caporali), nested "…".
- **Numbers:** comma decimal, point grouping — `3,14`, `1.234.567`.
- **Dates:** `26 maggio 2026` (month lowercase); with weekday `lunedì 26 maggio 2026` (weekday lowercase).
- **Dash:** the em-dash (lineetta) `—` is standard for asides.
- **Alphabet:** keep `à è é ì ò ó ù`; use elision apostrophes (`l'evento`, `dell'oro`).
- **Address:** formal *Lei* by default; *tu* when casual.
{{ end }}
{{ if target_language == "pt" }}
- **Variant:** default to European Portuguese (pt-PT); if the target is `pt-BR`, follow Brazilian norms.
- **Quotation marks:** «…» in pt-PT, "…" in pt-BR; nested swap the other style.
- **Numbers:** comma decimal, point grouping — `3,14`, `1.234.567`.
- **Dates:** `26 de maio de 2026` (month lowercase) or `26/05/2026`.
- **Dash:** the em-dash (travessão) `—` for asides and dialogue is standard.
- **Alphabet:** keep `á â ã à ç é ê í ó ô õ ú`.
- **Address:** polite by default — *o senhor / a senhora* or *você* in pt-PT, *você* in pt-BR.
{{ end }}
{{ if target_language == "nl" }}
- **Quotation marks:** primary "…", nested '…' (the older „…" still appears in print).
- **Numbers:** comma decimal, point grouping — `3,14`, `1.234.567` (a thin space is also used).
- **Dates:** `26 mei 2026` (month lowercase) or `26-05-2026` (hyphens, not dots).
- **Dash:** the spaced en-dash `–` (gedachtestreepje) for asides; a comma is often preferred.
- **Capitalization:** sentence case for headings; nouns, months and weekdays lowercase.
- **Address:** formal *u* by default; *je / jij* for casual copy.
- **Style:** Dutch compounds are natural (`olieprijs`, `marktopening`).
{{ end }}
{{ if target_language == "sv" }}
- **Quotation marks:** primary ”…” (a closing-style double on both sides), nested '…'; »…» also occurs.
- **Numbers:** comma decimal, non-breaking-space grouping — `3,14`, `1 234 567`.
- **Dates:** ISO `2026-05-26` is the everyday Swedish numeric form; the long form is `26 maj 2026` (month lowercase).
- **Dash:** the spaced en-dash `–` (tankstreck) for asides and ranges.
- **Alphabet:** keep `å ä ö`; months and weekdays are lowercase.
- **Address:** use *du* throughout — Swedish addresses readers informally even in business; *ni* is dated.
{{ end }}
{{ if target_language == "da" }}
- **Quotation marks:** primary »…« (or „…"), nested ›…‹.
- **Numbers:** comma decimal, point grouping — `3,14`, `1.234.567` (a space is also used).
- **Dates:** `26. maj 2026` (day takes a period, month lowercase) or `26.05.2026`.
- **Dash:** the spaced en-dash `–` (tankestreg) for asides and ranges.
- **Alphabet:** keep `æ ø å`; months and weekdays are lowercase; nouns are lowercase (unlike German).
- **Address:** *du* by default; reserve *De* for very formal contexts.
{{ end }}
{{ if target_language == "no" }}
- **Quotation marks:** primary «…», nested '…'.
- **Numbers:** comma decimal, non-breaking-space grouping — `3,14`, `1 234 567`.
- **Dates:** `26. mai 2026` (day takes a period, month lowercase) or `26.05.2026`.
- **Dash:** the spaced en-dash `–` (tankestrek) for asides and ranges.
- **Alphabet:** keep `æ ø å`; months and weekdays are lowercase. Write Bokmål unless Nynorsk is requested.
- **Address:** *du* by default; *De* is archaic.
{{ end }}
{{ if target_language == "fi" }}
- **Quotation marks:** primary ”…” (a closing-style double on both sides), nested '…'.
- **Numbers:** comma decimal, non-breaking-space grouping — `3,14`, `1 234 567`.
- **Dates:** numeric `26.5.2026` (no leading zeros); long `26. toukokuuta 2026` (month in the partitive, lowercase).
- **Dash:** the spaced en-dash `–` (ajatusviiva) for asides and ranges.
- **Alphabet:** keep `ä ö å`; months, weekdays, languages and nationalities are lowercase.
- **Address:** *sinä* (informal) is widely acceptable; use *te* for distinctly formal copy. Mirror the source otherwise.
- **Style:** Finnish is agglutinative — inflect words with the correct case endings (e.g. *Helsingissä*) instead of relying on prepositions or English word order.
{{ end }}
{{ if target_language == "ru" }}
- **Script:** write in Cyrillic throughout.
- **Quotation marks:** primary «…» (ёлочки), nested „…".
- **Numbers:** comma decimal, non-breaking-space grouping — `3,14`, `1 234 567`.
- **Dates:** `26 мая 2026` (month in the genitive, lowercase) or `26.05.2026`.
- **Dash:** тире (em-dash) `—`, set with spaces, is common — including as a copula linking subject and predicate (`X — это Y`).
- **Names:** use established Cyrillic forms for well-known names; identifiers and Latin acronyms stay Latin.
- **Address:** formal *вы* by default; *ты* when casual.
{{ end }}
{{ if target_language == "uk" }}
- **Script:** write in Ukrainian Cyrillic — it uses `ґ є і ї` and not `ё ы э`.
- **Quotation marks:** primary «…», nested „…".
- **Numbers:** comma decimal, non-breaking-space grouping — `3,14`, `1 234 567`.
- **Dates:** `26 травня 2026` (month in the genitive, lowercase) or `26.05.2026`.
- **Dash:** тире (em-dash) `—`, set with spaces, including as a copula (`X — це Y`).
- **Names:** established Ukrainian forms; identifiers and Latin acronyms stay Latin. Prefer Ukrainian-native lexis over Russian-influenced wording.
- **Address:** formal *ви* by default; *ти* when casual.
{{ end }}
{{ if target_language == "cs" }}
- **Quotation marks:** primary „…", nested ‚…'.
- **Numbers:** comma decimal, non-breaking-space grouping — `3,14`, `1 234 567`.
- **Dates:** numeric `26. 5. 2026` (periods with spaces); long `26. května 2026` (month in the genitive, lowercase).
- **Dash:** the spaced en-dash `–` (pomlčka) for asides; a closed en-dash for ranges.
- **Alphabet:** keep `á č ď é ě í ň ó ř š ť ú ů ý ž`. Avoid leaving a one-letter preposition (`k s v z o u a i`) at a line end — bind it to the next word with a non-breaking space.
- **Address:** formal *vy* by default; *ty* when casual.
{{ end }}
{{ if target_language == "sk" }}
- **Quotation marks:** primary „…", nested ‚…'.
- **Numbers:** comma decimal, non-breaking-space grouping — `3,14`, `1 234 567`.
- **Dates:** numeric `26. 5. 2026` (periods with spaces); long `26. mája 2026` (month in the genitive, lowercase).
- **Dash:** the spaced en-dash `–` (pomlčka) for asides; a closed en-dash for ranges.
- **Alphabet:** keep `á ä č ď é í ĺ ľ ň ó ô ŕ š ť ú ý ž`. Bind one-letter prepositions (`k s v z o u a i`) to the next word with a non-breaking space.
- **Address:** formal *vy* by default; *ty* when casual.
{{ end }}
{{ if target_language == "hu" }}
- **Quotation marks:** primary „…", nested »…« (reversed guillemets).
- **Numbers:** comma decimal, space grouping — `3,14`, `1 234 567`.
- **Dates:** big-endian with periods — `2026. május 26.` (month lowercase, trailing period) or `2026. 05. 26.`.
- **Dash:** the spaced en-dash `–` (gondolatjel) for asides, not the em-dash.
- **Alphabet:** keep `á é í ó ö ő ú ü ű` (note the long `ő ű`); months, weekdays and language names are lowercase.
- **Names:** Hungarian personal names are family-name-first (`Nagy János`); keep foreign names in their original order.
- **Address:** formal *ön / maga* by default; *te* when casual.
- **Style:** Hungarian is agglutinative with vowel harmony — attach the correct suffixes rather than imitating English prepositions.
{{ end }}
{{ if target_language == "tr" }}
- **Quotation marks:** primary "…"; «…» is the formal alternative.
- **Numbers:** comma decimal, point grouping — `3,14`, `1.234.567`.
- **Dates:** `26 Mayıs 2026` (month names are capitalized in Turkish) or `26.05.2026`; with weekday `26 Mayıs 2026 Pazartesi`.
- **Dash:** prefer commas or parentheses for asides; the long dash mainly introduces dialogue.
- **Alphabet:** keep `ç ğ ı i İ ö ş ü` and respect the dotted/dotless distinction (`i/İ`, `ı/I`). Attach suffixes to proper nouns after an apostrophe (`İstanbul'da`) and follow vowel harmony.
- **Address:** formal *siz* by default; *sen* when casual.
{{ end }}
{{ if target_language == "ar" }}
- **Direction:** Arabic is right-to-left; write it naturally and let the renderer handle direction. Numerals stay left-to-right within the text.
- **Quotation marks:** «…» or "…".
- **Numbers:** use Western digits (`0-9`) for technical and business copy and keep the source's decimal style; group thousands with a comma (`1,234,567`).
- **Punctuation:** use the Arabic comma `،` and Arabic question mark `؟`.
- **Dates:** `26 مايو 2026` (Gregorian month name) or `26/05/2026`; use the Gregorian calendar.
- **Dash:** Arabic does not use a dash for apposition — use the Arabic comma `،` or a colon; use the hyphen `-` in headings.
- **Names:** keep identifiers and Latin acronyms Latin; for a high-profile name a one-time Arabic gloss in parentheses on first mention is acceptable.
- **Address:** use the formal, respectful register by default.
{{ end }}
{{ if target_language != "en" && target_language != "pl" && target_language != "de" && target_language != "es" && target_language != "fr" && target_language != "it" && target_language != "pt" && target_language != "nl" && target_language != "sv" && target_language != "da" && target_language != "no" && target_language != "fi" && target_language != "ru" && target_language != "uk" && target_language != "cs" && target_language != "sk" && target_language != "hu" && target_language != "tr" && target_language != "ar" }}
No dedicated section is defined for this target. Apply general professional-translation rules:
- Use the quotation marks, decimal mark, digit grouping, and date format that an educated native reader of {{ target_name }} expects.
- Use the script and full diacritics of {{ target_name }}; render established proper names by its convention, and keep identifiers and Latin acronyms as written.
- Use the dash and apposition style native to {{ target_name }} rather than copying the source's.
- When in doubt, prefer conservative, faithful accuracy over creative localization.
{{ end }}

# Source text

{{ content }}

# Output

Output ONLY the finished {{ target_name }} translation as plain text — it is the deliverable itself, so add no labels, preamble, commentary, surrounding quotes or JSON wrapper. Keep the source's structure, markup and tokens intact.
""";
}

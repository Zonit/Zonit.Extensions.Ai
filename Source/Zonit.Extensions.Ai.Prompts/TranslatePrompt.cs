using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Translates text into a target language as a native writer would — preserving meaning, structure
/// and non-translatable tokens, while localizing punctuation, numbers, currencies, units, dates and
/// typography to the conventions of the target language.
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
/// <para>
/// The template itself follows the rules it is judged by, because a prompt's style is mirrored in
/// its output: it states the task instead of a persona, contains no em or en dash characters (Polish
/// and others forbid them, so the prompt names dashes instead of showing them), shows formats with
/// placeholders such as <c>&lt;amount&gt; &lt;code&gt;</c> rather than real currencies or units (a
/// literal example is copied into the translation), uses plain labels without bold or capitalised
/// mandates, and keeps the output contract at the end.
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
Translate the text inside <source_text> into {{ target_name }} so that a {{ target_name }} reader receives it as if it had been written in {{ target_name }}: the author's meaning, intent, tone and structure, written with the spelling, punctuation, number, currency, unit, date and typography conventions of {{ target_name }}.
{{ if source_language != "" }}The source text is written in {{ source_name }}.{{ else }}Identify the source language first.{{ end }}

# What stays as in the source

- Meaning and completeness: everything the source says, and nothing added. Titles, headings and labels are translated too, and brand and product names keep their original form. A phrase without a literal equivalent is rendered by its intent.
- Layout and markup: the same paragraphs, line breaks, headings, lists, tables and order. Markdown, HTML or XML tags, attributes and code fences are copied unchanged, and only the human-readable text between them is translated.
- Tokens that are not language: source code, commands, file paths, URLs, e-mail addresses, hashtags, handles, emoji and placeholders such as {0}, {{ "{{name}}" }}, %s, :id and $VAR are copied character for character, in place.
- Names and identifiers: people, organisations, brands, products, model and part numbers, codes and version strings keep their spelling. When the target uses another script, well-known names follow the convention of its section, and identifiers and Latin acronyms stay Latin.
- Values: every quantity, amount, percentage and measurement keeps its exact value and precision. An amount stays in its source currency, and a time of day stays in its source time zone with its zone label.

# What follows {{ target_name }}

- Numbers: the decimal mark, digit grouping and percent spacing of the section below apply to every number in the text, including numbers inside ranges, prices, percentages and compound units. A number written next to an identifier, field name or code is still a number: the identifier stays as written and the number takes the target format.
- Currency: an amount is written in the form the section below gives, with the ISO code of its currency. A currency symbol in the source becomes the ISO code of the same currency, unless the section below keeps symbols. Each currency keeps one form throughout the text.
- Units: a metric or SI symbol stays as written, and a rate built on one keeps its slash: <amount> <code>/<symbol>. An imperial or US customary unit (ounce, pound, gallon, barrel, bushel and the like) has no {{ target_name }} symbol, so its abbreviation is always written out as the {{ target_name }} word for that unit, and a rate built on it takes the per form given below. This holds in price lists, tables and headings as much as in prose. A trading or energy code built from the initials of several words stays as written, together with its rate slash, even when it measures an imperial quantity, because {{ target_name }} readers use such codes unchanged. Each unit keeps one form throughout the text.
- Dates: the date order and month names given below.
- Idiom and register: an idiom, set phrase or metaphor becomes its natural {{ target_name }} counterpart. The register of the source (formal, casual, marketing, technical, legal) is kept, and where {{ target_name }} forces a choice of address the section below gives the default.
- Typography: quotation marks, asides, separators and capitalisation follow the section below as each sentence is written.
- Consistency: one source term gets one translation throughout, and the text keeps one voice.

# {{ target_name }} conventions

{{ if target_language == "en" }}
- Quotation marks: curly double quotes for a quote, curly single quotes inside it.
- Numbers: decimal point and a comma between thousands (3.14 and 1,234,567). The percent sign follows the number without a space (12.5%).
- Currency: keep the source's choice between a symbol before the amount without a space and <code> <amount> with a space.
- Units: English unit abbreviations are native and stay as written, including a rate written as <code>/<unit>; a unit from another language takes its English name.
- Dates: US English by default (month name, day, year), British English when requested (day, month name, year); month and weekday names are capitalised.
- Asides: the em dash is natural in English; US style sets it without spaces, British style prefers a spaced en dash.
- Style: sentence case in body text and the source's case in headings; loanwords keep their diacritics.
{{ end }}
{{ if target_language == "pl" }}
- Quotation marks: „…” for a quote, «…» for a quote inside it.
- Numbers: decimal comma and a space between thousands (3,14 and 1 234 567). The percent sign follows the number without a space (12,5%).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word za: <amount> <code> za <unit>, with the unit as a Polish noun in the accusative.
- Dates: numeric day.month.year with two digits for day and month; the long form is the day, the month name in the genitive in lowercase, and the year.
- Asides: a comma or parentheses carry every aside and pause, including one the source marks with an em dash or en dash, because Polish prose uses neither dash. A heading label keeps a hyphen.
- Alphabet: full diacritics (ą ć ę ł ń ó ś ź ż).
- Address: the formal Pan and Pani by default, and ty only when the source is clearly casual.
- Style: natural Polish word order, and loanwords only where Polish readers use them.
{{ end }}
{{ if target_language == "de" }}
- Quotation marks: „…“ for a quote, ‚…‘ inside it (»…« in print and Swiss usage).
- Numbers: decimal comma and a point or thin space between thousands (3,14 and 1.234.567). The percent sign follows the number after a space (12,5 %).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word je: <amount> <code> je <unit>.
- Dates: numeric day.month.year; the long form is the day with a period, the capitalised month name and the year.
- Asides: a spaced en dash (Gedankenstrich) carries an aside.
- Alphabet: ä ö ü ß (ss in Swiss German), and every noun capitalised.
- Address: the formal Sie by default, and du for clearly casual or youth-facing text.
- Style: a well-formed compound is preferred to a loose paraphrase.
{{ end }}
{{ if target_language == "es" }}
- Quotation marks: «…» for a quote, curly double quotes inside it.
- Punctuation: a question or an exclamation opens with an inverted mark (¿…? and ¡…!).
- Numbers: in Spain a decimal comma and a point between thousands (3,14 and 1.234.567); much of Latin America uses a decimal point and a comma between thousands. Spain is the default unless the target is a Latin American locale. The percent sign follows the number after a space (12,5 %).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word por: <amount> <code> por <unit>.
- Dates: the day, de, the month name in lowercase, de and the year, or numeric day/month/year.
- Asides: the em dash (raya) carries an aside and dialogue.
- Alphabet: á é í ó ú ü ñ.
- Address: the formal usted by default and tú when casual; ustedes for the plural, and vosotros only for informal Spain.
{{ end }}
{{ if target_language == "fr" }}
- Quotation marks: «…» with a non-breaking space inside each guillemet, curly double quotes inside a quote.
- Spacing: a non-breaking space before ; : ! ? and the closing guillemet, and after the opening one.
- Numbers: decimal comma and a non-breaking space between thousands (3,14 and 1 234 567). The percent sign follows the number after a non-breaking space (12,5 %).
- Currency: <amount> <code>, with a non-breaking space before the code.
- Units: a rate on an imperial or US customary unit takes the word par: <amount> <code> par <unit>.
- Dates: the day, the month name in lowercase and the year, with no comma; a weekday comes first, in lowercase.
- Asides: a spaced em dash (tiret cadratin) carries an aside, and an en dash marks a range.
- Alphabet: the full accent set (à â ç é è ê ë î ï ô û ù ü ÿ œ æ) and the typographic apostrophe ’.
- Address: the formal vous by default, and tu when casual.
{{ end }}
{{ if target_language == "it" }}
- Quotation marks: «…» (caporali) for a quote, curly double quotes inside it.
- Numbers: decimal comma and a point between thousands (3,14 and 1.234.567). The percent sign follows the number without a space (12,5%).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes al or alla, agreeing with the unit: <amount> <code> al <unit>.
- Dates: the day, the month name in lowercase and the year; a weekday comes first, in lowercase.
- Asides: the em dash (lineetta) carries an aside.
- Alphabet: à è é ì ò ó ù, and elision apostrophes.
- Address: the formal Lei by default, and tu when casual.
{{ end }}
{{ if target_language == "pt" }}
- Variant: European Portuguese (pt-PT) by default, Brazilian norms when the target is pt-BR.
- Quotation marks: «…» in pt-PT and curly double quotes in pt-BR; the other style inside a quote.
- Numbers: decimal comma and a point between thousands (3,14 and 1.234.567). The percent sign follows the number without a space (12,5%).
- Currency: <amount> <code> in pt-PT, and <code> <amount> in pt-BR, with a space between them.
- Units: a rate on an imperial or US customary unit takes the word por: <amount> <code> por <unit>.
- Dates: the day, de, the month name in lowercase, de and the year, or numeric day/month/year.
- Asides: the em dash (travessão) carries an aside and dialogue.
- Alphabet: á â ã à ç é ê í ó ô õ ú.
- Address: polite by default, o senhor or a senhora or você in pt-PT, and você in pt-BR.
{{ end }}
{{ if target_language == "nl" }}
- Quotation marks: curly double quotes for a quote, curly single quotes inside it („…” still appears in print).
- Numbers: decimal comma and a point or thin space between thousands (3,14 and 1.234.567). The percent sign follows the number without a space (12,5%).
- Currency: <code> <amount>, with a space after the code.
- Units: a rate on an imperial or US customary unit takes the word per: <code> <amount> per <unit>.
- Dates: the day, the month name in lowercase and the year, or numeric day-month-year with hyphens.
- Asides: a spaced en dash (gedachtestreepje) carries an aside, though a comma is often preferred.
- Capitalisation: sentence case in headings; nouns, months and weekdays in lowercase.
- Address: the formal u by default, and je or jij for casual text.
- Style: Dutch compounds are natural.
{{ end }}
{{ if target_language == "sv" }}
- Quotation marks: ”…” (the closing double quote on both sides) for a quote, ’…’ inside it.
- Numbers: decimal comma and a non-breaking space between thousands (3,14 and 1 234 567). The percent sign follows the number after a space (12,5 %).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word per: <amount> <code> per <unit>.
- Dates: numeric year-month-day is the everyday form; the long form is the day, the month name in lowercase and the year.
- Asides: a spaced en dash (tankstreck) carries an aside and marks a range.
- Alphabet: å ä ö; months and weekdays in lowercase.
- Address: du throughout, also in business text.
{{ end }}
{{ if target_language == "da" }}
- Quotation marks: »…« (or „…”) for a quote, ›…‹ inside it.
- Numbers: decimal comma and a point or space between thousands (3,14 and 1.234.567). The percent sign follows the number after a space (12,5 %).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes pr.: <amount> <code> pr. <unit>.
- Dates: the day with a period, the month name in lowercase and the year, or numeric day.month.year.
- Asides: a spaced en dash (tankestreg) carries an aside and marks a range.
- Alphabet: æ ø å; months, weekdays and nouns in lowercase.
- Address: du by default, and De only in very formal text.
{{ end }}
{{ if target_language == "no" }}
- Quotation marks: «…» for a quote, ’…’ inside it.
- Numbers: decimal comma and a non-breaking space between thousands (3,14 and 1 234 567). The percent sign follows the number after a space (12,5 %).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word per: <amount> <code> per <unit>.
- Dates: the day with a period, the month name in lowercase and the year, or numeric day.month.year.
- Asides: a spaced en dash (tankestrek) carries an aside and marks a range.
- Alphabet: æ ø å; months and weekdays in lowercase. Bokmål unless Nynorsk is requested.
- Address: du by default.
{{ end }}
{{ if target_language == "fi" }}
- Quotation marks: ”…” (the closing double quote on both sides) for a quote, ’…’ inside it.
- Numbers: decimal comma and a non-breaking space between thousands (3,14 and 1 234 567). The percent sign follows the number after a space (12,5 %).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit puts the unit in the ablative: <amount> <code> <unit in the ablative>.
- Dates: numeric day.month.year without leading zeros; the long form is the day with a period, the month name in the partitive in lowercase, and the year.
- Asides: a spaced en dash (ajatusviiva) carries an aside and marks a range.
- Alphabet: ä ö å; months, weekdays, languages and nationalities in lowercase.
- Address: sinä is widely acceptable and te is for distinctly formal text; otherwise follow the source.
- Style: words take the correct case endings instead of prepositions or English word order.
{{ end }}
{{ if target_language == "ru" }}
- Script: Cyrillic throughout.
- Quotation marks: «…» for a quote, „…“ inside it.
- Numbers: decimal comma and a non-breaking space between thousands (3,14 and 1 234 567). The percent sign follows the number without a space (12,5%).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word за: <amount> <code> за <unit>, with the unit in the accusative.
- Dates: the day, the month name in the genitive in lowercase, and the year, or numeric day.month.year.
- Asides: a spaced em dash (тире) carries an aside and also links a subject to its predicate.
- Names: established Cyrillic forms for well-known names; identifiers and Latin acronyms stay Latin.
- Address: the formal вы by default, and ты when casual.
{{ end }}
{{ if target_language == "uk" }}
- Script: Ukrainian Cyrillic, which uses ґ є і ї and has no ё ы э.
- Quotation marks: «…» for a quote, „…“ inside it.
- Numbers: decimal comma and a non-breaking space between thousands (3,14 and 1 234 567). The percent sign follows the number without a space (12,5%).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word за: <amount> <code> за <unit>, with the unit in the accusative.
- Dates: the day, the month name in the genitive in lowercase, and the year, or numeric day.month.year.
- Asides: a spaced em dash (тире) carries an aside and also links a subject to its predicate.
- Names: established Ukrainian forms; identifiers and Latin acronyms stay Latin. Ukrainian native vocabulary is preferred to Russian-influenced wording.
- Address: the formal ви by default, and ти when casual.
{{ end }}
{{ if target_language == "cs" }}
- Quotation marks: „…“ for a quote, ‚…‘ inside it.
- Numbers: decimal comma and a non-breaking space between thousands (3,14 and 1 234 567). The percent sign follows the number after a space (12,5 %), and without a space when the percentage is used as an adjective.
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word za: <amount> <code> za <unit>.
- Dates: numeric day. month. year with a space after each period; the long form is the day with a period, the month name in the genitive in lowercase, and the year.
- Asides: a spaced en dash (pomlčka) carries an aside, and a closed en dash marks a range.
- Alphabet: á č ď é ě í ň ó ř š ť ú ů ý ž. A one-letter preposition (k s v z o u a i) is bound to the next word with a non-breaking space.
- Address: the formal vy by default, and ty when casual.
{{ end }}
{{ if target_language == "sk" }}
- Quotation marks: „…“ for a quote, ‚…‘ inside it.
- Numbers: decimal comma and a non-breaking space between thousands (3,14 and 1 234 567). The percent sign follows the number after a space (12,5 %), and without a space when the percentage is used as an adjective.
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes the word za: <amount> <code> za <unit>.
- Dates: numeric day. month. year with a space after each period; the long form is the day with a period, the month name in the genitive in lowercase, and the year.
- Asides: a spaced en dash (pomlčka) carries an aside, and a closed en dash marks a range.
- Alphabet: á ä č ď é í ĺ ľ ň ó ô ŕ š ť ú ý ž. A one-letter preposition (k s v z o u a i) is bound to the next word with a non-breaking space.
- Address: the formal vy by default, and ty when casual.
{{ end }}
{{ if target_language == "hu" }}
- Quotation marks: „…” for a quote, »…« inside it.
- Numbers: decimal comma and a space between thousands (3,14 and 1 234 567). The percent sign follows the number without a space (12,5%).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit keeps a slash with the Hungarian unit name: <amount> <code>/<unit>.
- Dates: year first, with periods: the year, the month name in lowercase and the day, each followed by a period, or numeric year. month. day.
- Asides: a spaced en dash (gondolatjel) carries an aside.
- Alphabet: á é í ó ö ő ú ü ű; months, weekdays and language names in lowercase.
- Names: a Hungarian name puts the family name first; a foreign name keeps its order.
- Address: the formal ön or maga by default, and te when casual.
- Style: suffixes with vowel harmony carry what English expresses with prepositions.
{{ end }}
{{ if target_language == "tr" }}
- Quotation marks: curly double quotes for a quote; «…» is the formal alternative.
- Numbers: decimal comma and a point between thousands (3,14 and 1.234.567). The percent sign comes before the number (%12,5).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit puts the unit first with başına: <unit> başına <amount> <code>.
- Dates: the day, the capitalised month name and the year, with a weekday after them, or numeric day.month.year.
- Asides: a comma or parentheses carry an aside; a long dash mainly introduces dialogue.
- Alphabet: ç ğ ı i İ ö ş ü, keeping the dotted and dotless i apart. A suffix on a proper noun follows an apostrophe, and suffixes follow vowel harmony.
- Address: the formal siz by default, and sen when casual.
{{ end }}
{{ if target_language == "ar" }}
- Direction: Arabic is written right to left and numerals stay left to right; the renderer handles direction.
- Quotation marks: «…» or curly double quotes.
- Numbers: Western digits (0 to 9) in technical and business text, the decimal style of the source, and a comma between thousands (1,234,567). The percent sign follows the number without a space (12.5%).
- Currency: <amount> <code>, with a space before the code.
- Units: a rate on an imperial or US customary unit takes لل before the unit: <amount> <code> لل<unit>.
- Punctuation: the Arabic comma ، and question mark ؟.
- Dates: the day, the Gregorian month name and the year, or numeric day/month/year; the Gregorian calendar throughout.
- Asides: the Arabic comma or a colon carry an aside; a heading keeps a hyphen.
- Names: identifiers and Latin acronyms stay Latin; a high-profile name may get a one-time Arabic gloss in parentheses on first mention.
- Address: the formal, respectful register by default.
{{ end }}
{{ if target_language != "en" && target_language != "pl" && target_language != "de" && target_language != "es" && target_language != "fr" && target_language != "it" && target_language != "pt" && target_language != "nl" && target_language != "sv" && target_language != "da" && target_language != "no" && target_language != "fi" && target_language != "ru" && target_language != "uk" && target_language != "cs" && target_language != "sk" && target_language != "hu" && target_language != "tr" && target_language != "ar" }}
- Use the quotation marks, decimal mark, digit grouping, percent spacing, date format, currency position and per form for units that an educated native reader of {{ target_name }} expects.
- Use the script and full diacritics of {{ target_name }}, and the punctuation {{ target_name }} uses for an aside.
- Where a convention is uncertain, prefer faithful accuracy over creative localisation.
{{ end }}

<source_text>
{{ content }}
</source_text>

Return the finished {{ target_name }} translation as plain text, starting with its first word, with no label, preamble, commentary, surrounding quotes or JSON wrapper. Keep the structure, markup and tokens of the source.
""";
}

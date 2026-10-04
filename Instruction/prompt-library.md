# Ready-made prompts (`Zonit.Extensions.Ai.Prompts`)

`Zonit.Extensions.Ai.Prompts` is a separate package of reusable, production-grade prompt
templates. When the task matches one of these, install the package and use it instead of writing
a prompt from scratch.

```bash
dotnet add package Zonit.Extensions.Ai.Prompts
```

The prompts are normal `PromptBase<TResponse>` classes, so they work with the standard
`ai.GenerateAsync(model, prompt)` call and return a typed result. Pick a model the usual way
(see [`models.md`](./models.md)).

## Available prompts

| Prompt | Returns | Purpose |
| :--- | :--- | :--- |
| `TranslatePrompt` | `string` (the translated text) | Translate text into a target language as a native writer would |

## TranslatePrompt

Translates text and localizes punctuation, numbers, dates and typography to the conventions of
the target language. It preserves layout, markup, code, URLs and placeholders. It returns the
translated text directly as a `string` (no JSON wrapper). Per-language rules cover the major European
languages plus Russian, Ukrainian, Turkish and Arabic; any other target falls back to general
translation rules.

Each language section states, explicitly, how that culture writes:

- **Numbers:** decimal mark, digit grouping and percent spacing. The same format applies inside
  ranges, prices and compound units (`12,5%` in Polish, `12,5 %` in German and French, `%12,5` in
  Turkish), and to a number written next to an identifier or field name (the identifier stays).
- **Currencies:** an amount takes the ISO code of its currency, placed as the culture writes it
  (`$4,327.29` in English becomes `4 327,29 USD` in Polish). Amounts are never converted and keep
  their decimals.
- **Units:** an imperial or US customary unit is written out in the target language, with the
  culture's form for a rate (`USD/gal` becomes `USD za galon` in Polish and `USD je Gallone` in
  German). Metric and SI symbols keep their slash (`EUR/MWh`), and codes built from initials
  (`MMBtu`) stay unchanged. Values are never converted.

A currency or unit is written the same way every time within one text.

The template is written to avoid prompt patterns that leak into output: it states the task instead
of a persona, contains no em or en dash characters, shows formats with placeholders
(`<amount> <code>`) rather than real currencies or units, and uses plain labels.

```csharp
using Zonit.Extensions.Ai.Prompts;

// Target accepts any ISO 639-1 or culture code (a string converts implicitly to Culture).
var result = await ai.GenerateAsync(
    new GPT5(),
    new TranslatePrompt { Content = "Hello world!", Target = "pl" });

string translated = result.Value;   // "Witaj świecie!" — translated text as a plain string
```

Properties:

| Property | Type | Default | Notes |
| :--- | :--- | :--- | :--- |
| `Content` | `string` (required) | | Text to translate |
| `Target` | `Culture` (required) | | Target language, e.g. `"pl"`, `"de-DE"` |
| `Source` | `Culture` | auto-detect | Leave unset to detect the source language |
| `Notes` | `string?` | `null` | Author's notes for this translation: context, terminology, a part that needs care. Any language. Unset → no notes section in the prompt |

```csharp
// Explicit source language (otherwise auto-detected)
new TranslatePrompt
{
    Content = text,
    Source  = "en",
    Target  = "de-DE",
};
```

### Notes from the author

`Notes` passes what the author knows about the text: a term to keep, a part a model tends to get
wrong. The prompt places them in their own block right before the source text; they apply together
with the built-in rules, and where a note and a rule disagree, the note decides. Write each note as
a plain statement of what the translation should do.

```csharp
new TranslatePrompt
{
    Content = signal,
    Target  = "pl",
    Notes   = "The price list follows the same rules as the prose.",
};
```

A note helps most where a cheaper model is weakest. On a market signal with a price block
(GPT-6 Luna, Low, 40 runs each), Luna wrote a half-translated rate such as `USD/funt` in 19 runs
without a note and in 1 to 3 runs with one; even the two-word note `tłumacz bloki cen` worked.
Claude Sonnet 5.5 made no such error with or without a note.

Each call is independent, so a pipeline can translate the same text into many languages in
parallel.

More prompts will be added to this package over time. When one fits the task, prefer it over a
hand-written prompt.

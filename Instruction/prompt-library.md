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

```csharp
// Explicit source language (otherwise auto-detected)
new TranslatePrompt
{
    Content = text,
    Source  = "en",
    Target  = "de-DE",
};
```

Each call is independent, so a pipeline can translate the same text into many languages in
parallel.

More prompts will be added to this package over time. When one fits the task, prefer it over a
hand-written prompt.

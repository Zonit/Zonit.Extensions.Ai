# Writing a prompt (`*Prompt.cs`)

A prompt is a typed class. Its public properties are both the data and the Scriban template
variables; the generic argument is the structured response type the model must return.

For prompts that already exist (translation and more), check
[`prompt-library.md`](./prompt-library.md) before writing your own.

## Template

```csharp
using System.ComponentModel;
using Zonit.Extensions.Ai;

public sealed class TranslatePrompt : PromptBase<TranslateResponse>
{
    public required string Content  { get; init; }
    public required string Language { get; init; }

    // Properties are exposed to the template as snake_case.
    public override string Prompt => """
        Translate the text below into {{ language }}.
        Preserve tone and formatting.

        {{ content }}
        """;
}

[Description("Translation result.")]
public sealed class TranslateResponse
{
    [Description("The translated text.")]
    public required string TranslatedText { get; init; }

    [Description("Detected source language (ISO-639-1).")]
    public string? DetectedLanguage { get; init; }
}
```

## Rules

- Inherit `PromptBase<TResponse>` and name the file `*Prompt.cs`.
- Declare inputs as public properties (`required` where mandatory). They are available in the
  template as `snake_case` (`RecipientName` becomes `{{ recipient_name }}`).
- Override `Prompt` with the template. Scriban supports `{{ for x in items }} ... {{ end }}`,
  `{{ if cond }} ... {{ end }}`, whitespace control `{{~ ... ~}}`, and `{{ list.size }}`.
- `TResponse` is the structured-output contract. Annotate it and its members with
  `[Description(...)]`, which flows into the JSON Schema. Use `required` for mandatory fields and
  a nullable type for optional ones.

## Calling

```csharp
Result<TranslateResponse> r = await ai.GenerateAsync(
    new GPT5(), new TranslatePrompt { Content = "Hello", Language = "Polish" }, ct);

string text = r.Value.TranslatedText;   // typed. Do not parse JSON and do not check IsSuccess.
```

## Files, one-offs, images

```csharp
// Attach files (images, PDFs). Asset is from Zonit.Extensions.
new MyPrompt { Files = [new Asset(bytes, "doc.pdf")] }

// Quick one-off without a class
await ai.GenerateAsync(new GPT5(), new SimplePrompt<MyDto>("Summarise: " + text), ct);

// Image prompt: inherit ImagePromptBase (returns Asset), or SimpleImagePrompt for a one-off
public sealed class PosterPrompt : ImagePromptBase
{
    public required string Topic { get; init; }
    public override string Prompt => "Minimalist poster about {{ topic }}";
}
```

Models and reasoning are in [`models.md`](./models.md). The result and cost shape is in
[`results.md`](./results.md).

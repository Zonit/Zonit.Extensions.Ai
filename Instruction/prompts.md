# Writing a prompt (`*Prompt.cs`)

A prompt is a typed class. Its public properties are both the data and the Scriban template
variables; the generic argument is the structured response type the model must return.

For prompts that already exist (translation and more), check
[`prompt-library.md`](./prompt-library.md) before writing your own.

## Template

```csharp
using System.ComponentModel;
using Zonit.Extensions.Ai;

public sealed class SentimentPrompt : PromptBase<SentimentResponse>
{
    public required string Text { get; init; }

    // Properties are exposed to the template as snake_case.
    public override string Prompt => """
        Classify the sentiment of the text below as positive, neutral or negative.

        {{ text }}
        """;
}

[Description("Sentiment classification.")]
public sealed class SentimentResponse
{
    [Description("positive, neutral or negative")]
    public required string Sentiment { get; init; }

    [Description("Confidence from 0.0 to 1.0.")]
    public double Confidence { get; init; }
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
- For a plain-text answer, use `PromptBase<string>` — the model's text is returned verbatim with
  no JSON step. Prefer this when the deliverable is just text (e.g. a translation), so output can
  never fail JSON parsing.

## Calling

```csharp
Result<SentimentResponse> r = await ai.GenerateAsync(
    new GPT5(), new SentimentPrompt { Text = "I love this!" }, ct);

string sentiment = r.Value.Sentiment;   // typed. Do not parse JSON and do not check IsSuccess.
```

## Files, one-offs

```csharp
// Attach files (images, PDFs). Asset is from Zonit.Extensions.
new MyPrompt { Files = [new Asset(bytes, "doc.pdf")] }

// Quick one-off without a class
await ai.GenerateAsync(new GPT5(), new SimplePrompt<MyDto>("Summarise: " + text), ct);
```

## Image and video generation

Image and video generation use **dedicated prompt types** — you never hand-roll an
`IPrompt<Asset>`. The overloads accept only `IImagePrompt` / `IVideoPrompt`, so a text
prompt can't be passed to image/video generation by mistake. The shape is always the same:
a `Text` description plus optional source media.

```csharp
// Image — text-to-image, and image-to-image (edit) by adding a source Image
await ai.GenerateAsync(imageModel, new ImagePrompt { Text = "a red bicycle" }, ct);
await ai.GenerateAsync(imageModel, new ImagePrompt { Text = "make it snowy", Image = source }, ct);

// Video — text-to-video, image-to-video (Image), video-to-video / edit (Video)
await ai.GenerateAsync(videoModel, new VideoPrompt { Text = "a butterfly over flowers" }, ct);
await ai.GenerateAsync(videoModel, new VideoPrompt { Text = "slow zoom in", Image = photo }, ct);
```

Whether a model actually accepts an image or video source is enforced centrally against
the model's declared input channels (`ILlm.Input`): passing a video to a model that only
does text/image throws before any API call. A generation with neither text nor a source
file is rejected too.

For a **templated/typed** prompt (Scriban parameters), inherit `ImagePromptBase` /
`VideoPromptBase`:

```csharp
public sealed class PosterPrompt : ImagePromptBase
{
    public required string Topic { get; init; }
    public override string Prompt => "Minimalist poster about {{ topic }}";
}
```

Models and reasoning are in [`models.md`](./models.md). The result and cost shape is in
[`results.md`](./results.md).

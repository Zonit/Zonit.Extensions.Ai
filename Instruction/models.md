# Models and capabilities

A model is a small strongly-typed class that implements `ILlm` plus one or more capability
interfaces. The interface it implements decides which `GenerateAsync` overload accepts it, so
the compiler stops you from, for example, asking an embedding model to generate an image.

| Interface | Enables |
| :--- | :--- |
| `ILlm` | Base contract: name, context window, pricing, capabilities |
| `IAgentLlm` | Tool-calling and the agent loop (most chat models implement this) |
| `IReasoningLlm` | Configurable reasoning effort, summary, verbosity |
| `IImageLlm` | Image generation |
| `IEmbeddingLlm` | Text embeddings |
| `IAudioLlm` | Audio transcription (speech → text) |
| `ISpeechLlm` | Speech synthesis / TTS (text → speech) |
| `IVideoLlm` | Video generation |
| `IFast` | Opt-in fast inference tier (premium pricing) |

## Picking a model

Concrete model classes live in the provider package under its `Llm/` folder. Use IntelliSense;
do not invent or memorise model names, because they change every release. Verified examples at
the time of writing: OpenAI `GPT5`, `GPT52`, `O3`, `GPTImage15`, `TextEmbedding3Large`,
`GPT4oTranscribe`; Anthropic `Sonnet5`, `Opus5`, `Haiku45`. For the capability each package provides,
see [`providers.md`](./providers.md).

> 📋 For the complete, always-current list of **every** model — provider, context window,
> modalities, capabilities and price (including cache) — see the generated
> [`llms.md`](./llms.md). It is produced from the model types by a test, so it
> never goes stale; do not edit it by hand.

Select the model in one place and pass it to `GenerateAsync`.

```csharp
ILlm model = quality switch
{
    Quality.Low    => new GPT5Mini(),
    Quality.Medium => new GPT5(),
    Quality.High   => new GPT52(),
    _              => new GPT5(),
};
var result = await ai.GenerateAsync(model, prompt, ct);
```

## Speech (text-to-speech)

`ISpeechLlm` models turn **text into audio** — the mirror of `IAudioLlm` transcription. Install a
TTS provider (`Zonit.Extensions.Ai.ElevenLabs`, register with `AddAiElevenLabs()`), pick a model,
set the voice and output format on the instance, then pass the text:

```csharp
using Zonit.Extensions.Ai.ElevenLabs;

var speech = new ElevenMultilingualV2
{
    Voice  = ElevenVoices.Rachel,               // any voice id — see below
    Format = ElevenAudioFormat.Mp3_44100_128,   // enum, not a raw string
};

Result<Asset> audio = await ai.GenerateAsync(speech, "Cześć, jak się masz?", ct);
await File.WriteAllBytesAsync("out.mp3", audio.Value.Data, ct);   // Asset carries bytes in .Data
```

The configuration lives on the model object (same convention as image models), so the positional
call takes only the text. Cost is per input character: `ai.CalculateCost(speech, text.Length)`.

### Continuous, consistent narration

Each `GenerateAsync` is a **separate synthesis**, so both the voice character and the intonation
reset between calls — audible as "jumps" when you render a script line-by-line. Two knobs fix that,
and both are **on by default**:

**1. Seed (`ElevenLabsSpeechBase.Seed`) — consistent voice character.** A seed (0..4 294 967 295) makes
sampling *best-effort deterministic*: the same text + seed + settings yields the same audio
(determinism is not guaranteed). By default **the model auto-generates one random seed per instance**
and sends it on every call — so reusing one model object across a script already keeps the voice
steady, even if you never touch the property. Set your own value for full run-to-run reproducibility,
or set `Seed = null` to opt out (ElevenLabs then randomizes per call and does **not** return the seed).

**2. `SpeechPrompt.PreviousText` / `NextText` — continuous intonation.** Feed each line the surrounding
text as context (ElevenLabs `previous_text` / `next_text`); it is not spoken and not billed, but lets
the narrator flow out of the previous line and into the next.

**Easiest: hand the whole script to the batch overload.** It returns one recording per line and fills
the context automatically — `previous_text` = the *last sentence* of the previous line, `next_text` =
the *first sentence* of the next — while the model's shared seed keeps the character consistent:

```csharp
var speech = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel }; // random seed auto-set & reused

string[] script =
[
    "She slowly opened the ancient door.",
    "I could not believe my eyes. The whole room was made of gold.",
    "We had finally found it.",
];

IReadOnlyList<Result<Asset>> takes = await ai.GenerateAsync(speech, script, ct);
for (var i = 0; i < takes.Count; i++)
    await File.WriteAllBytesAsync($"line{i}.mp3", takes[i].Value.Data, ct);
```

Prefer manual control? Use the single-line `SpeechPrompt` overload and set the context yourself:

```csharp
var line = await ai.GenerateAsync(speech, new SpeechPrompt
{
    Text         = lines[i],
    PreviousText = i > 0 ? lines[i - 1] : null,
    NextText     = i < lines.Count - 1 ? lines[i + 1] : null,
}, ct);
```

The boundary sentences are also **character-capped** (~300, whole-word) taken from the correct end —
the *tail* of the previous line, the *head* of the next — so unpunctuated text (one giant "sentence")
never blows up the context.

### Emotion & delivery (audio tags — no separate "prompt" field)

There is no dedicated emotion parameter — delivery is directed **inside the text**, plus the numeric
`Style` / `Stability` on the model. On the **`ElevenV3`** model you write inline
[audio tags](https://elevenlabs.io/blog/v3-audiotags): cues in square brackets that the model interprets
and performs. Because the direction rides in the string, a plain script array already carries it (other
models — Multilingual v2, Turbo, Flash — do **not** interpret tags and would read the brackets literally,
so use `ElevenV3` for this).

**Tags are free-form, not a fixed list.** They are descriptive cues the model *interprets*, so you are
not limited to a fixed vocabulary — experiment with plain descriptions. In particular:

- **Multi-word tags work:** `[resigned tone]`, `[laughs softly]`, `[speaking quickly]`, `[drawn out]`.
  So yes — something like `[nervous]` or `[nervous, rushed]` is fine; it's your description, not a keyword.
- **Stack them:** you can place several tags in one line/sentence, e.g. `[nervous][whispers]`.
- It is **best-effort / experimental** (v3 is alpha): "there are likely many more effective tags — try
  descriptive emotional states and actions and keep what lands."

Rough menu of what people use (any descriptive variant works):

| Category | Examples |
| :--- | :--- |
| Emotion | `[excited]` `[nervous]` `[sad]` `[angry]` `[happily]` `[calm]` `[curious]` `[frustrated]` `[sorrowful]` |
| Delivery / tone | `[whispers]` `[shouts]` `[rushed]` `[drawn out]` `[cheerfully]` `[deadpan]` `[sarcastic]` `[resigned tone]` `[<x> accent]` |
| Non-verbal / reactions | `[laughs]` `[laughs softly]` `[sighs]` `[gasps]` `[gulps]` `[clears throat]` `[stammers]` `[crying]` |
| Sound effects | `[gunshot]` `[clapping]` `[explosion]` |

**Caveat — the tag must fit the voice.** A calm voice asked to `[shout]`, or a shouting voice asked to
`[whisper]`, won't land well; pick a voice whose character can reach the direction (and Instant Voice
Clones handle v3 better than Professional ones).

**Usage — one tag per example:**

```csharp
var voice = new ElevenV3 { Voice = ElevenVoices.Rachel };   // v3 = audio-tag support

// emotion — tag at the start sets the mood for the line
await ai.GenerateAsync(voice, "[excited] We actually did it!");

// non-verbal reaction — mid-sentence
await ai.GenerateAsync(voice, "Well [sighs] I suppose we can try again.");

// delivery + multi-word tag
await ai.GenerateAsync(voice, "[whispers] Don't tell anyone.");
await ai.GenerateAsync(voice, "Fine [resigned tone], have it your way.");

// stacked tags on one line
await ai.GenerateAsync(voice, "[nervous][rushed] We need to leave. Now.");

// a whole directed scene in one call — each prompt rendered exactly as given, no stitching
IReadOnlyList<Result<Asset>> takes = await ai.GenerateAsync(voice,
[
    new SpeechPrompt { Text = "[excited] We did it!" },
    new SpeechPrompt { Text = "[whispers] ...but don't tell anyone." },
]);
```

### Subtitles / captions — timestamps

For a voice-over on a subtitled video, use `GenerateWithTimestampsAsync` — it returns the audio
**plus per-character timing**, and `ToWords()` rolls those up to word cues you can turn into an SRT/VTT:

```csharp
Result<SpeechTimestamps> r = await ai.GenerateWithTimestampsAsync(speech, "Hello there, welcome back.");

await File.WriteAllBytesAsync("vo.mp3", r.Value.Audio.Data);      // the audio
foreach (var w in r.Value.ToWords())                              // word-level cues
    Console.WriteLine($"{w.Start:mm\\:ss\\.fff} → {w.End:mm\\:ss\\.fff}  {w.Word}");

// r.Value.Characters gives the raw per-character alignment if you need finer control.
```

Only providers that return alignment (ElevenLabs) support this; others throw `NotSupportedException`.

### Model capability matrix (ElevenLabs)

| Model | Audio tags (`[excited]`…) | Request stitching (prev/next) | Max chars |
| :--- | :---: | :---: | ---: |
| `ElevenV3` | ✅ | ❌ (v3 doesn't support it) | ~5 000 |
| `ElevenMultilingualV2` | ❌ | ✅ | 10 000 |
| `ElevenTurboV2_5` / `ElevenFlashV2_5` | ❌ | ✅ | 40 000 |
| `ElevenTurboV2` / `ElevenFlashV2` | ❌ | ✅ | 30 000 |

The provider enforces this: on v3 it **omits** `previous_text`/`next_text` automatically (they'd be
ignored), so the same `SpeechPrompt` / script code works on any model — v3 just expresses through tags
instead of stitching. `SupportsAudioTags` / `SupportsRequestStitching` on the model expose these flags.

**Choosing a model.** Concrete `Eleven*` classes live under the package's `Llm/` folder and appear
in [`llms.md`](./llms.md). Rough guide: `ElevenV3` (most expressive, 70+ languages),
`ElevenMultilingualV2` (quality, 29 languages), `ElevenTurboV2_5` (balanced), `ElevenFlashV2_5`
(lowest latency). Pick with IntelliSense.

**Voice.** A voice is a string id, because providers expose thousands of premade voices plus your
own cloned/designed ones — too many to enumerate. `ElevenVoices` is a small catalog of premade ids
for convenience (`ElevenVoices.Rachel`), but **any** id works, including a cloned-voice id from your
account: `new ElevenMultilingualV2 { Voice = "xxxx" }`. (Professional/cloned voices
may require a higher ElevenLabs subscription tier; the API returns a clear 403 if your plan can't use
one.)

**Format.** `ElevenAudioFormat` is an enum (fixed set, IDE-discoverable) whose members map to the
API's `output_format` wire value — MP3 at several bitrates, raw PCM, and μ-law for telephony.

**Tuning delivery.** `Stability`, `SimilarityBoost`, `Style` and `UseSpeakerBoost` are init
properties on the model with sensible defaults; override them for more or less expressive output.

### Creating your own model

The `Eleven*` classes cover the current engines, but the set of models changes — you can add one
(or a preconfigured variant) yourself without waiting for a package update. Derive from
`ElevenLabsSpeechBase` and set `Name` to the ElevenLabs `model_id`:

```csharp
using Zonit.Extensions.Ai.ElevenLabs;

// A brand-new engine id not yet in the package:
public sealed class ElevenSomethingNew : ElevenLabsSpeechBase
{
    public override string Name => "eleven_something_new";   // the model_id sent to the API
    public override int MaxCharacters => 40_000;             // reject longer text up front
    public override decimal PricePerThousandCharacters => 0.30m; // your plan's rate, for cost math
}
```

Because it derives from the same base, it flows through `ai.GenerateAsync(speech, text)` exactly like
the built-ins. Often you don't even need a subclass — just build a preconfigured instance where you
need it (voice and delivery are init properties):

```csharp
var narrator = new ElevenMultilingualV2 { Voice = ElevenVoices.Adam, Stability = 0.7 };
var line = await ai.GenerateAsync(narrator, "Rozdział pierwszy.", ct);
```

Custom subclasses you define in your own project do **not** appear in the generated
[`llms.md`](./llms.md) (that catalog reflects only the provider packages) — that is expected; the
catalog lists what ships in the box, your models are yours.

## Reasoning models

Reasoning models expose effort, summary and verbosity through typed properties. The effort enum is
per tier, so a model only accepts the levels its API actually supports (passing an unsupported level
is a compile-time error). OpenAI GPT-5.0–5.5 / o-series use `OpenAiReasonEffort`
(none/low/medium/high); GPT-5.6 (Sol / Terra / Luna) use `OpenAiReasonEffortExtended`, which adds
`Xhigh` and `Max`.

```csharp
var r = await ai.GenerateAsync(
    new GPT52
    {
        Reason    = OpenAiReasonEffort.High,     // None, Low, Medium, High
        Verbosity = OpenAiReasoningBase.VerbosityType.Low,   // Low, Medium, High
    },
    prompt, ct);

// O-series models always reason.
await ai.GenerateAsync(new O3 { Reason = OpenAiReasonEffort.High }, "Prove...", ct);

// GPT-5.6 (Sol / Terra / Luna) adds two deeper effort levels: Xhigh and Max.
await ai.GenerateAsync(new Sol56 { Reason = OpenAiReasonEffortExtended.Xhigh }, prompt, ct);
```

Reasoning tokens are reported on `MetaData.Usage.ReasoningTokens`. See [`results.md`](./results.md).

## Fast mode (`IFast`)

Some models offer a faster inference tier with the same weights at premium pricing. Cost
calculation switches to the fast rate automatically when it is selected.

```csharp
await ai.GenerateAsync(new Opus5 { Speed = SpeedType.Fast }, "Draft a release note.", ct);
```

## Prompt caching (Anthropic)

Anthropic models cache the stable prefix of a request (system prompt, tool catalogue, the
conversation so far) server-side and replay it on later turns at ~10% of the input price. Turn it
on with the `Cache` property — it is **off by default** (`Cache.None`).

```csharp
using Zonit.Extensions.Ai.Anthropic;   // the Cache enum

await ai.Agent(
        new Opus5 { Cache = Cache.FiveMinutes },   // None | FiveMinutes | OneHour
        new ResearchPrompt { Topic = "EU AI Act" })
    .AddTool<SearchTool>()
    .RunAsync();
```

| TTL | When to use it |
| :--- | :--- |
| `Cache.None` | One-off calls with no shared prefix (default). |
| `Cache.FiveMinutes` | Agent and chat loops where turns land within a few minutes. |
| `Cache.OneHour` | Long-running sessions or chats with idle gaps over five minutes (beta TTL). |

The first turn *writes* the prefix (1.25× input for `FiveMinutes`, 2× for `OneHour`); every later
turn *reads* it at ~10% of input price, so caching is net-positive from the second turn onward —
the exact shape of an agent run, where the system prompt and tool definitions repeat every turn.
**Enable it whenever you build an agent, run a multi-turn chat, or fire repeated calls that share
a large prompt prefix.** No per-call wiring is needed: once `Cache` is set the library places the
cache breakpoints (tools, system, the two most recent turns) automatically, and cached /
cache-write tokens are reported and priced separately on `MetaData.Usage` (see
[`results.md`](./results.md)). Caching is Anthropic-only; other providers ignore the property.

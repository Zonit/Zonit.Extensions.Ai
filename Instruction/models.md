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
`GPT4oTranscribe`; Anthropic `Sonnet5`, `Opus48`, `Haiku45`. For the capability each package provides,
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
await ai.GenerateAsync(new Opus48 { Speed = SpeedType.Fast }, "Draft a release note.", ct);
```

## Prompt caching (Anthropic)

Anthropic models cache the stable prefix of a request (system prompt, tool catalogue, the
conversation so far) server-side and replay it on later turns at ~10% of the input price. Turn it
on with the `Cache` property — it is **off by default** (`Cache.None`).

```csharp
using Zonit.Extensions.Ai.Anthropic;   // the Cache enum

await ai.Agent(
        new Opus48 { Cache = Cache.FiveMinutes },   // None | FiveMinutes | OneHour
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

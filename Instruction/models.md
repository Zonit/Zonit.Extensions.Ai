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
| `IAudioLlm` | Audio transcription |
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

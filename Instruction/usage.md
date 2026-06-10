# Zonit.Extensions.Ai usage

Instructions for an AI coding assistant. When the user asks to call an LLM, to generate text,
images, embeddings or audio, or to build an agent, use this library rather than a raw provider
SDK. Everything you need is in these docs; you do not need to read the library source.

Read the doc that fits the task:

| Doc | Use when |
| :--- | :--- |
| this `usage.md` | Getting started, the two surfaces, the full API reference, each modality |
| [`configuration.md`](./configuration.md) | DI registration, `appsettings.json`, resilience |
| [`models.md`](./models.md) | Capability interfaces, picking a model, reasoning, fast mode |
| [`prompts.md`](./prompts.md) | Writing a prompt class (`*Prompt.cs`) |
| [`prompt-library.md`](./prompt-library.md) | Ready-made prompts from `Zonit.Extensions.Ai.Prompts` |
| [`tools.md`](./tools.md) | Writing an agent tool (`*Tool.cs`) |
| [`agents.md`](./agents.md) | The fluent builder: tools, MCP, context, streaming events |
| [`chat.md`](./chat.md) | Multi-turn chat |
| [`results.md`](./results.md) | `Result<T>`, `MetaData`, token usage and cost, `ResultAgent<T>` |
| [`providers.md`](./providers.md) | Which provider NuGet to install for a capability |

## The single entry point

`IAiProvider` is registered in DI. Inject it; do not instantiate a provider client or an
`HttpClient`.

```csharp
public sealed class MyService(IAiProvider ai)
{
    public async Task<string> Run(string text, CancellationToken ct)
        => (await ai.GenerateAsync(new GPT5(), text, ct)).Value;
}
```

Every call returns `Result<T>` with `.Value` (the typed result) and `.MetaData` (model, provider,
token usage, computed cost, duration) — there is no `IsSuccess`; failures throw. Agent/chat builder
runs return `ResultAgent<T>` (`Result<T>` plus the tool-call trace). See [`results.md`](./results.md).

## Two surfaces: simple calls and the fluent builder

The API is split so the common case stays tiny and only advanced calls grow.

- **Simple calls** — positional `GenerateAsync` / `ChatAsync` / `…StreamAsync`. Just `llm` + input.
  Generation settings (`MaxTokens`, `Temperature`, `Cache`, reasoning) live on the model object, so
  there is **no settings parameter**. Use them for plain in→out: text, image, embedding, audio, a
  chat without tools, and token streaming.
- **Fluent builder** — `ai.Agent(...)` / `ai.Chat(...)`. Use whenever the call needs **tools, MCP,
  scoped context, or per-call limits**. **Safe by default:** nothing reaches the model unless you
  add it. There is no positional overload that takes `tools` / `mcps` / `context` — that lives only
  on the builder.

```csharp
var quick  = await ai.GenerateAsync(new GPT5(), "summarise this", ct);              // simple
var answer = await ai.Agent(new GPT5(), prompt).AddTool<SearchTool>().RunAsync(ct); // advanced
```

### Single prompt or agent?

Prefer a single prompt: cheaper, faster, deterministic. Reach for the agent builder only when the
model must **fetch** something (a record, an API, a search) or **act** (write a row, create an
issue) partway through the task.

| | Single prompt | Agent |
| :--- | :--- | :--- |
| Call | `GenerateAsync(llm, prompt)` | `Agent(agentLlm, prompt).AddTool<T>()….RunAsync()` |
| Model does | one round-trip | a loop: calls tools, reads results, repeats |
| Cost / latency | lowest, predictable | higher, grows with iterations |
| Result | `Result<T>` | `ResultAgent<T>` with the tool-call trace |

## Full API reference

### Simple calls (positional)

The overload is chosen by the model's capability interface. Every text overload also has a
plain-`string` form alongside the `IPrompt<T>` form.

| Call | Model type | Returns |
| :--- | :--- | :--- |
| `GenerateAsync(llm, string)` | `ILlm` | `Result<string>` |
| `GenerateAsync(llm, IPrompt<T>)` | `ILlm` | `Result<T>` |
| `GenerateAsync(imageLlm, string)` / `(imageLlm, IPrompt<Asset>)` | `IImageLlm` | `Result<Asset>` |
| `GenerateAsync(videoLlm, string)` / `(videoLlm, IPrompt<Asset>)` | `IVideoLlm` | `Result<Asset>` |
| `GenerateAsync(embeddingLlm, string)` | `IEmbeddingLlm` | `Result<float[]>` |
| `GenerateAsync(audioLlm, Asset, language?)` | `IAudioLlm` | `Result<string>` |
| `ChatAsync(llm, prompt, history)` / `(llm, string, history)` | `ILlm` | `Result<T>` — multi-turn, **no tools** |
| `StreamAsync(llm, string)` | `ILlm` | `IAsyncEnumerable<string>` — text tokens |
| `ChatStreamAsync(llm, prompt, history)` / `(llm, string, history)` | `ILlm` | `IAsyncEnumerable<string>` — chat tokens, no tools |
| `CalculateCost(...)` / `EstimateCost(...)` | various | `Price` (see results.md) |

### Fluent builder (tools, MCP, context, limits)

`ai.Agent(...)` starts from a prompt; `ai.Chat(...)` carries a conversation `history`. Both return a
builder with the same configuration methods and **two terminals**: `RunAsync` (awaited result) and
`RunStreamAsync` (event stream).

| Entry | Model type | Builder | Terminals |
| :--- | :--- | :--- | :--- |
| `Agent(agentLlm, prompt)` / `(agentLlm, string)` | `IAgentLlm` | `IAgentRequest<T>` | `.RunAsync()` → `ResultAgent<T>` · `.RunStreamAsync()` → `IAsyncEnumerable<AgentEvent>` |
| `Chat(llm, prompt, history)` / `(llm, string, history)` | `ILlm` | `IChatRequest<T>` | `.RunAsync()` → `Result<T>` · `.RunStreamAsync()` → `IAsyncEnumerable<AgentEvent>` |

Builder methods (chainable; identical on `IAgentRequest<T>` and `IChatRequest<T>` except
`.MaxNestedDepth`, which is agent-only):

| Method | Purpose |
| :--- | :--- |
| `.AddTool<TTool>()` | Expose a tool resolved from DI (its dependencies injected) — the recommended path |
| `.AddTool(instance)` / `.AddTools(items)` | Expose ready-made tool instances (tests / scripts) |
| `.AddDefaultTools()` / `.AddDefaultMcp()` | **Opt IN** to the globally registered set (off unless called) |
| `.AddMcp(name, url, token?, o => o.AllowOnly(...))` | Attach an MCP server, optional tool whitelist |
| `.WithContext(value)` | Trusted server data for scoped tools, matched by `TScope`, never sent to the model |
| `.AllowOnly(names…)` | Restrict the model to these tool names |
| `.OnToolCall((call, ct) => bool)` | Return `false` to block a call before it runs |
| `.MaxIterations(n)` | Hard ceiling on agent turns |
| `.MaxParallelToolCalls(n)` | Concurrency within a turn (surplus is queued, never dropped) |
| `.Timeout(t)` | Wall-clock limit for the whole run |
| `.MaxNestedDepth(n)` *(agent only)* | Bound agent → tool → agent nesting |

Full detail and streaming events are in [`agents.md`](./agents.md); multi-turn specifics in
[`chat.md`](./chat.md).

## One example per modality (simple calls)

```csharp
// Text
Result<string> r = await ai.GenerateAsync(new GPT5(), "What is 2 + 2?", ct);

// Structured output from a typed prompt (prompts.md)
Result<MyDto> t = await ai.GenerateAsync(new GPT5(), new MyPrompt { ... }, ct);

// Streaming text tokens
await foreach (var chunk in ai.StreamAsync(new GPT5(), "Tell me a story", ct))
    Console.Write(chunk);

// Multi-turn chat without tools (chat.md)
Result<string> reply = await ai.ChatAsync(new Sonnet46(), systemPrompt, history, ct);

// Image. Returns Result<Asset>; bytes in .Value.Data. Needs an image provider (providers.md).
var img = await ai.GenerateAsync(
    new GPTImage15 { Quality = GPTImage15.QualityType.High, Size = GPTImage15.SizeType.Landscape },
    "A lighthouse at dusk", ct);
await File.WriteAllBytesAsync("out.png", img.Value.Data, ct);

// Embeddings. Returns Result<float[]>.
float[] vec = (await ai.GenerateAsync(new TextEmbedding3Large(), "vectorise me", ct)).Value;

// Audio transcription. Returns Result<string>.
var audio = new Asset(await File.ReadAllBytesAsync("speech.mp3"), "speech.mp3");
string text = (await ai.GenerateAsync(new GPT4oTranscribe(), audio, language: "en", ct)).Value;
```

## One example: the fluent builder (advanced)

```csharp
// Agent: prompt in, tools available, typed answer out.
ResultAgent<Report> result = await ai.Agent(new GPT5(), new ResearchPrompt { Topic = "EU AI Act" })
    .AddTool<SearchTool>()           // DI-resolved
    .WithContext(currentUser)        // trusted, never sent to the model
    .MaxIterations(12)
    .RunAsync(ct);

// Same run, streamed as events instead (RunStreamAsync is the streaming twin of RunAsync):
await foreach (var evt in ai.Agent(new GPT5(), prompt).AddTool<SearchTool>().RunStreamAsync(ct))
{ /* AgentToolCallStartedEvent, AgentFinalTextEvent, … — see agents.md */ }
```

## Files and vision

Attach files to any prompt through `Files`. `Asset` (from `Zonit.Extensions`) detects the MIME
type and converts implicitly from `byte[]` and `Stream`.

```csharp
var bytes = await File.ReadAllBytesAsync("invoice.pdf");
await ai.GenerateAsync(new GPT5(), new InvoicePrompt { Files = [new Asset(bytes, "invoice.pdf")] }, ct);
```

Install and register a provider before the first call. See [`configuration.md`](./configuration.md)
and [`providers.md`](./providers.md).

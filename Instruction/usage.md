# Zonit.Extensions.Ai usage

Instructions for an AI coding assistant. When the user asks to call an LLM, to generate text,
images, embeddings or audio, or to build an agent, use this library rather than a raw provider
SDK. Everything you need is in these docs; you do not need to read the library source.

Read the doc that fits the task:

| Doc | Use when |
| :--- | :--- |
| this `usage.md` | Getting started, the unified API, calling each modality |
| [`configuration.md`](./configuration.md) | DI registration, `appsettings.json`, resilience |
| [`models.md`](./models.md) | Capability interfaces, picking a model, reasoning, fast mode |
| [`prompts.md`](./prompts.md) | Writing a prompt class (`*Prompt.cs`) |
| [`prompt-library.md`](./prompt-library.md) | Ready-made prompts from `Zonit.Extensions.Ai.Prompts` |
| [`tools.md`](./tools.md) | Writing an agent tool (`*Tool.cs`) |
| [`agents.md`](./agents.md) | Agents: tool-calling, MCP, streaming events |
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

Every call returns `Result<T>` with `.Value` (the typed result) and `.MetaData` (model,
provider, token usage, computed cost, duration). `Result<T>` exposes `.Value` and `.MetaData`
only. There is no `IsSuccess` property; failures throw. See [`results.md`](./results.md).

## Prompt or agent

| | Single prompt | Agent |
| :--- | :--- | :--- |
| Call | `GenerateAsync(llm, prompt)` | `GenerateAsync(agentLlm, prompt, tools, mcps)` |
| Model does | one round-trip | a loop: calls tools, reads results, repeats |
| Use when | the model has everything it needs in the prompt | the task needs live data or actions the model cannot perform alone |
| Cost and latency | lowest, predictable | higher, grows with iterations |
| Result | `Result<T>` | `ResultAgent<T>` with the tool-call trace |

Prefer a single prompt. It is cheaper, faster, more deterministic and easier to test, and a
well-structured prompt that already carries the needed context gives the best result for a
self-contained task. Use an agent only when the model must fetch something (a record, an API
response, a search) or take an action (write to a database, create an issue) partway through the
task. The test: if the model needs to do or fetch something mid-task, use an agent; otherwise
use a prompt.

## The unified API

The overload is chosen by the model's capability interface.

| Call | Model type | Returns |
| :--- | :--- | :--- |
| `GenerateAsync(llm, string)` | `ILlm` | `Result<string>` |
| `GenerateAsync(llm, IPrompt<T>)` | `ILlm` | `Result<T>` |
| `GenerateAsync(imageLlm, string)` | `IImageLlm` | `Result<Asset>` |
| `GenerateAsync(embeddingLlm, string)` | `IEmbeddingLlm` | `Result<float[]>` |
| `GenerateAsync(audioLlm, Asset, language?)` | `IAudioLlm` | `Result<string>` |
| `GenerateAsync(videoLlm, string)` | `IVideoLlm` | `Result<Asset>` |
| `StreamAsync(llm, string)` | `ILlm` | `IAsyncEnumerable<string>` |
| `ChatAsync(...)` / `ChatStreamAsync(...)` | `ILlm` | `Result<T>` / tokens (see chat.md) |
| `GenerateAsync(agentLlm, prompt, tools, mcps, options)` | `IAgentLlm` | `ResultAgent<T>` (see agents.md) |
| `GenerateStreamAsync(agentLlm, ...)` | `IAgentLlm` | `IAsyncEnumerable<AgentEvent>` |
| `CalculateCost(...)` / `EstimateCost(...)` | various | `Price` (see results.md) |

## One example per modality

```csharp
// Text
Result<string> r = await ai.GenerateAsync(new GPT5(), "What is 2 + 2?", ct);

// Structured output from a typed prompt (prompts.md)
Result<MyDto> t = await ai.GenerateAsync(new GPT5(), new MyPrompt { ... }, ct);

// Streaming tokens
await foreach (var chunk in ai.StreamAsync(new GPT5(), "Tell me a story", ct))
    Console.Write(chunk);

// Image. Returns Result<Asset>; bytes in .Value.Data. Needs an image provider (providers.md).
var img = await ai.GenerateAsync(
    new GPTImage15 { Quality = GPTImage15.QualityType.High, Size = GPTImage15.SizeType.Landscape },
    "A lighthouse at dusk", ct);
await File.WriteAllBytesAsync("out.png", img.Value.Data, ct);

// Embeddings. Returns Result<float[]>.
float[] vec = (await ai.GenerateAsync(new TextEmbedding3Large(), "vectorise me", ct)).Value;

// Audio transcription. Returns Result<string>.
var audio = new Asset(await File.ReadAllBytesAsync("speech.mp3"), "speech.mp3");
string text = (await ai.GenerateAsync(new Whisper1(), audio, language: "en", ct)).Value;
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

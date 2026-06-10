<div align="center">

# Zonit.Extensions.Ai

**One typed, AOT-ready .NET API for every major AI provider: text, images, audio, embeddings, agents and tools.**

[![Build](https://github.com/Zonit/Zonit.Extensions.Ai/actions/workflows/PublishNuGet.yml/badge.svg)](https://github.com/Zonit/Zonit.Extensions.Ai/actions/workflows/PublishNuGet.yml)
[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.svg?label=NuGet)](https://www.nuget.org/packages/Zonit.Extensions.Ai)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.svg?label=downloads)](https://www.nuget.org/packages/Zonit.Extensions.Ai)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE.txt)

</div>

---

`Zonit.Extensions.Ai` talks to OpenAI, Anthropic, Google, xAI and a dozen other providers
through a single interface, `IAiProvider`. You pick a model as a strongly-typed class, pass a
typed prompt, and get back a typed result with usage and cost already calculated. The same
`GenerateAsync` call resolves to text, image, audio or embedding by the model you hand it; agents
and tool-driven chat use the fluent `ai.Agent(...)` / `ai.Chat(...)` builder. No separate clients,
no stringly-typed configuration, no provider-specific SDKs in your domain code.

```csharp
IAiProvider ai = /* injected */;

// Plain text
var answer = await ai.GenerateAsync(new GPT5(), "Summarise the CAP theorem in one sentence.");

// Typed, structured output (JSON Schema generated for you)
var review = await ai.GenerateAsync(new Sonnet46(), new CodeReviewPrompt { Diff = diff });
Console.WriteLine(review.Value.Severity);     // strongly typed
Console.WriteLine(review.MetaData.TotalCost); // cost, already computed
```

> 📖 **Guides:** [`Instruction/`](./Instruction). Task-focused docs that are also compiled into your AI
> assistant on install (see [AI-assistant ready](#ai-assistant-ready)).

## Everything in one library

One API covers every provider, every modality, and the agentic stack.

| | |
| :--- | :--- |
| **16 providers** | OpenAI · Anthropic · Google · xAI · DeepSeek · Mistral · Groq · Together · Fireworks · Cohere · Perplexity · Alibaba · Baidu · Zhipu · Moonshot · 01.AI |
| **Every modality** | text · structured output (JSON Schema) · images · embeddings · audio transcription · video · streaming |
| **Agentic** | typed tools · external MCP servers · parallel tool calls · streaming events · full audit trail |
| **Production** | token usage and computed cost (including nested) · retry, timeout, circuit-breaker · reasoning and fast modes · AOT- and trim-ready (`net10.0`) |
| **AI-assistant ready** | ships instructions for **GitHub Copilot, Claude Code and Cursor**; your coding agent learns the library on install ([details](#ai-assistant-ready)) |

## Table of contents

- [Why this library](#why-this-library)
- [AI-assistant ready](#ai-assistant-ready)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Core concepts](#core-concepts)
- [The unified API](#the-unified-api)
- [Working with modalities](#working-with-modalities)
- [Reasoning and fast mode](#reasoning-and-fast-mode)
- [Prompt caching](#prompt-caching)
- [Multi-turn chat](#multi-turn-chat)
- [Agents and tools](#agents-and-tools)
- [Ready-made prompts](#ready-made-prompts)
- [Cost tracking and estimation](#cost-tracking-and-estimation)
- [Configuration](#configuration)
- [Resilience](#resilience)
- [AOT and trimming](#aot-and-trimming)
- [Provider packages](#provider-packages)
- [Architecture](#architecture)
- [Requirements and license](#requirements-and-license)

---

## Why this library

- **One API, every provider.** Inject `IAiProvider` once. Switch a model class to switch
  providers; the call site does not change.
- **Typed prompts, typed answers.** Define a `PromptBase<TResponse>`. The library generates the
  JSON Schema, sends it as a structured-output request, and deserializes the reply into
  `TResponse`.
- **Scriban templating built in.** Prompt properties are exposed to the template as
  `snake_case`, with loops, conditionals and interpolation.
- **Agents with tools and MCP.** Typed tools (`ToolBase<TInput, TOutput>`), external MCP servers
  over HTTP/SSE, parallel tool execution, streaming events, and a complete audit trail on
  `ResultAgent<T>`.
- **Cost is a first-class value.** Every result carries token usage and computed cost. Agent
  runs also roll up the cost of nested AI calls made inside your tools.
- **Production resilience.** Retry, timeout and circuit-breaker via
  `Microsoft.Extensions.Http.Resilience`, configurable globally or per provider.
- **AOT- and trim-ready.** Targets `net10.0` and source-generates the structured-output path. See
  [AOT and trimming](#aot-and-trimming) for what is and is not AOT-safe.

---

## AI-assistant ready

When you install the package, a build-time step compiles a complete description of the library
into your repository, so GitHub Copilot, Claude Code and Cursor know how to use `IAiProvider`
correctly without being prompted. That includes which provider NuGet to install for a given
capability: ask for "generate images with GPT" and the assistant installs
`Zonit.Extensions.Ai.OpenAi` and writes the code.

- **Only for editors you use.** Files are written only for editors detected in the repo
  (`.claude/` or `CLAUDE.md`, `.cursor/`, `.vscode/`, or existing Copilot instructions). With no
  AI editor present, nothing is written.
- **One source, compiled per editor.** A single set of Markdown docs (the same files in
  [`Instruction/`](./Instruction)) is projected into each editor's native format:

  | Editor | Output |
  | :--- | :--- |
  | **Cursor** | `.cursor/rules/zonit-ai*.mdc`, scoped to `*Prompt.cs` and `*Tool.cs` via `globs` |
  | **GitHub Copilot** | `.github/instructions/zonit-ai*.instructions.md` (`applyTo`) and a block in `copilot-instructions.md` |
  | **Claude Code** | a Skill (`.claude/skills/zonit-extensions-ai/`) plus a pointer block in `CLAUDE.md` |
  | *(all)* | a guide tree under `.zonit/extensions/ai/` and a map at `.zonit/index.md` |

- **Safe and idempotent.** Blocks live between markers, so your own edits are never overwritten;
  nothing is rewritten unless it changed; the step is skipped on CI.
- **Control:** `<ZonitAiInstructions>false</ZonitAiInstructions>` disables it;
  `<ZonitAiEditors>auto|all|none|cursor;claude</ZonitAiEditors>` overrides detection.

---

## Installation

Install the core package plus the provider package(s) you need:

```powershell
# Core (required): DI, prompts, templating, agent runtime
dotnet add package Zonit.Extensions.Ai

# Add one or more providers
dotnet add package Zonit.Extensions.Ai.OpenAi
dotnet add package Zonit.Extensions.Ai.Anthropic
dotnet add package Zonit.Extensions.Ai.Google
```

The MCP client and agent runtime live in the core package; there is nothing extra to install for
tools or agents. See [Provider packages](#provider-packages) for the full list.

---

## Quick start

### 1. Store your keys

```json
// appsettings.json  (use User Secrets or environment variables in production)
{
  "Ai": {
    "OpenAi":    { "ApiKey": "sk-..." },
    "Anthropic": { "ApiKey": "sk-ant-..." }
  }
}
```

### 2. Register the providers

```csharp
using Zonit.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAiOpenAi();      // binds "Ai:OpenAi" automatically
builder.Services.AddAiAnthropic();   // binds "Ai:Anthropic"

var app = builder.Build();
```

`AddAi{Provider}()` registers the provider, binds its configuration section, and wires the core
services and a resilient `HttpClient`. Registration is idempotent, so it is safe to call from as
many modules or plugins as you like.

### 3. Inject `IAiProvider` and call

```csharp
internal sealed class Summariser(IAiProvider ai)
{
    public async Task<string> RunAsync(string text, CancellationToken ct)
    {
        var result = await ai.GenerateAsync(new GPT5(), $"Summarise:\n{text}", ct);
        return result.Value;
    }
}
```

The whole loop: choose a model, pass a prompt, read `result.Value`.

---

## Core concepts

### Models are capabilities, not strings

A model is a small class that implements `ILlm` and one or more capability interfaces. The
interface a model implements decides which `GenerateAsync` overload accepts it, so the compiler
stops you from, for example, asking an embedding model to generate an image.

| Capability interface | What it enables |
| :--- | :--- |
| `ILlm`          | Base contract: name, context window, pricing, capabilities |
| `IAgentLlm`     | Tool-calling and the agent loop (most chat models implement this) |
| `IReasoningLlm` | Configurable reasoning effort, summaries, verbosity |
| `IImageLlm`     | Image generation |
| `IEmbeddingLlm` | Text embeddings |
| `IAudioLlm`     | Audio transcription |
| `IVideoLlm`     | Video generation |
| `IFast`         | Opt-in fast inference tier (premium pricing) |

Concrete model classes (`GPT5`, `Sonnet46`, `GPTImage15`, `TextEmbedding3Large`) ship inside each
provider package and evolve with the providers. Discover them through IntelliSense or the
package's `Llm/` folder; each class carries its own context window, pricing and supported
endpoints. This README lists no model tables, because they go stale every release.

### Prompts are types

Inherit `PromptBase<TResponse>`. Public properties become both template variables and the data
the model sees; `TResponse` becomes the structured-output contract.

```csharp
using System.ComponentModel;
using Zonit.Extensions; // Culture value object

public sealed class SentimentPrompt : PromptBase<SentimentResponse>
{
    public required string Text { get; init; }

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

`[Description]` attributes are forwarded into the JSON Schema, so the model receives your intent
verbatim. For one-off calls without a class, use `SimplePrompt<T>`. For ready-made prompts,
see [Ready-made prompts](#ready-made-prompts).

### Results carry usage and cost

Every call returns `Result<T>`:

```csharp
public class Result<T>
{
    public required T        Value    { get; init; }  // the typed answer
    public required MetaData MetaData { get; init; }  // model, usage, cost, timing
}
```

`MetaData` exposes the model, provider, derived `PromptName`, `Duration`, `RequestId`, full
`TokenUsage`, and shortcuts such as `InputTokens`, `TotalTokens` and `TotalCost`. Costs are
`Price` value objects, computed from the model's own pricing.

---

## The unified API

All generation goes through `IAiProvider`, on **two surfaces**: simple positional calls for plain
in→out, and the fluent `Agent` / `Chat` builder for anything with tools, MCP, context or limits
(safe by default — no tool reaches the model unless you add it). Simple calls, selected by the
model's capability interface:

| Call | Model type | Returns |
| :--- | :--- | :--- |
| `GenerateAsync(llm, string)` / `(llm, IPrompt<T>)` | `ILlm`          | `Result<string>` / `Result<T>` |
| `GenerateAsync(imageLlm, string)` / `(…, IPrompt<Asset>)` | `IImageLlm`     | `Result<Asset>` |
| `GenerateAsync(videoLlm, string)` / `(…, IPrompt<Asset>)` | `IVideoLlm`     | `Result<Asset>` |
| `GenerateAsync(embeddingLlm, string)`   | `IEmbeddingLlm` | `Result<float[]>` |
| `GenerateAsync(audioLlm, Asset, language?)` | `IAudioLlm`     | `Result<string>` |
| `ChatAsync(llm, prompt, history)`       | `ILlm`          | `Result<T>` — multi-turn, no tools |
| `StreamAsync(llm, string)` · `ChatStreamAsync(llm, prompt, history)` | `ILlm` | `IAsyncEnumerable<string>` (tokens) |
| `CalculateCost(...)` / `EstimateCost(...)` | various     | `Price` |

For **tools, MCP, scoped context or per-call limits**, use the fluent builder —
`ai.Agent(agentLlm, prompt)` or `ai.Chat(llm, prompt, history)`, each terminated by `.RunAsync()`
(awaited result) or `.RunStreamAsync()` (event stream). Full reference in
[Agents and tools](#agents-and-tools). Every text entry point also has a plain-`string` overload.

---

## Working with modalities

### Text and structured output

```csharp
// Free text
Result<string> reply = await ai.GenerateAsync(new GPT5(), "What is 2 + 2?");
Console.WriteLine(reply.Value); // "4"

// Structured. SentimentResponse is filled from the model's JSON.
Result<SentimentResponse> r = await ai.GenerateAsync(
    new Sonnet46(),
    new SentimentPrompt { Text = "I love this!" });

Console.WriteLine(r.Value.Sentiment);  // "positive"
Console.WriteLine(r.MetaData.PromptName);   // "Translate"
```

### Streaming text

```csharp
await foreach (var chunk in ai.StreamAsync(new GPT5(), "Tell me a short story."))
    Console.Write(chunk);
```

### Files and vision

`Asset` (from `Zonit.Extensions`) detects the MIME type and converts implicitly from `byte[]`
and `Stream`. Attach files to any prompt through `Files`:

```csharp
public sealed class InvoicePrompt : PromptBase<InvoiceData>
{
    public override string Prompt => "Extract the invoice fields from the attached document.";
}

var bytes = await File.ReadAllBytesAsync("invoice.pdf");
var result = await ai.GenerateAsync(
    new GPT5(),
    new InvoicePrompt { Files = [new Asset(bytes, "invoice.pdf")] });
```

### Image generation

Each image model declares its own `QualityType` and `SizeType` enums, mapped to the exact API
values, so you cannot pass an unsupported combination.

```csharp
var image = await ai.GenerateAsync(
    new GPTImage15
    {
        Quality = GPTImage15.QualityType.High,
        Size    = GPTImage15.SizeType.Landscape,
    },
    "A lighthouse at dusk, dramatic clouds, photorealistic");

await File.WriteAllBytesAsync("lighthouse.png", image.Value.Data);
```

### Embeddings

```csharp
Result<float[]> embedding = await ai.GenerateAsync(
    new TextEmbedding3Large(), "vectorise me");

float[] vector = embedding.Value;
```

### Audio transcription

```csharp
var audio = new Asset(await File.ReadAllBytesAsync("speech.mp3"), "speech.mp3");
var text  = await ai.GenerateAsync(new GPT4oTranscribe(), audio, language: "en");
Console.WriteLine(text.Value);
```

---

## Reasoning and fast mode

Reasoning models expose effort, summary and verbosity controls through typed properties:

```csharp
var result = await ai.GenerateAsync(
    new GPT52
    {
        Reason    = OpenAiReasoningBase.ReasonType.High,
        Verbosity = OpenAiReasoningBase.VerbosityType.Low,
    },
    new ProofPrompt { Statement = "root 2 is irrational" });
```

Some models offer a fast inference tier (`IFast`): the same weights at higher throughput and
premium pricing. Cost calculation switches to the fast rate automatically when you select it.

```csharp
var fast = await ai.GenerateAsync(
    new Opus48 { Speed = SpeedType.Fast },
    "Draft a release note for v10.");
```

---

## Prompt caching

Anthropic models support **prompt caching**: the large, stable part of a request — the system
prompt, the tool catalogue, the conversation so far — is cached server-side and replayed on later
turns at a fraction of the input price. Enable it per model with the `Cache` property; it is
**off by default**.

```csharp
using Zonit.Extensions.Ai.Anthropic;   // the Cache enum

// Agents, chat loops and any repeated-prefix calls benefit most.
var result = await ai.Agent(
        new Opus48 { Cache = Cache.FiveMinutes },     // None (default) | FiveMinutes | OneHour
        new ResearchPrompt { Topic = "EU AI Act" })
    .AddTool<SearchTool>()
    .AddTool<SaveNoteTool>()
    .RunAsync();
```

| TTL | Use it when |
| :--- | :--- |
| `Cache.None` | One-off calls with no shared prefix (default). |
| `Cache.FiveMinutes` | Agent and chat loops where turns land within a few minutes. |
| `Cache.OneHour` | Long-running sessions or chats with idle gaps over five minutes (beta TTL). |

The economics: the **first** turn *writes* the prefix to the cache at a premium (1.25× input for
`FiveMinutes`, 2× for `OneHour`), so it is slightly more expensive; every turn after that *reads*
that prefix at ~10% of the input price. Caching is therefore net-positive from the second turn
onward — exactly the shape of an agent run, where the system prompt and tool definitions repeat
every turn. No per-call wiring is needed: once `Cache` is set, the library manages the cache
breakpoints (tools, system, and the two most recent turns) automatically. Cached and cache-write
tokens are reported separately on `MetaData.Usage` and priced at the model's cached rates, so
`TotalCost` stays accurate. Caching is Anthropic-specific; other providers ignore the property.

---

## Multi-turn chat

`ChatAsync` is the conversational counterpart to `GenerateAsync`. The conversation lives in a
`ChatMessage[]` of `User` and `Assistant` records; the prompt supplies the system instruction. In
chat mode `prompt.Text` is the system message, the inverse of single-shot `GenerateAsync`, where
it is the user message.

```csharp
var history = new ChatMessage[]
{
    new User("Why does my deployment hang?"),
    new Assistant("Often a missing source-gen override. What error do you see?"),
    new User("CS0534 on .NET 10."),
};

var result = await ai.ChatAsync(
    new Sonnet46(),
    new HelpdeskPrompt { Product = "Zonit.Ai" },   // system instruction
    history);

Console.WriteLine(result.Value);
```

For a tool-driven conversation, switch to the fluent builder —
`ai.Chat(llm, prompt, history).AddTool<T>()….RunAsync()` — the turn then routes through the agent
runner and the result's `.Value` is a `ResultAgent<T>` (see below). For token-by-token output
without tools, use `ChatStreamAsync`. Native multi-turn message arrays are used for OpenAI,
Anthropic, xAI (Grok) and Google (Gemini); other providers fall back to a flattened transcript.

---

## Agents and tools

An agent drives the model in a loop: the model requests tool calls, the library runs them in
parallel, feeds the results back, and repeats until the model returns a final structured answer,
all behind one `await`. Prefer a single prompt for self-contained tasks and use an agent when the
model needs to fetch data or take actions mid-task.

```csharp
var result = await ai.Agent(new GPT5(), new ResearchPrompt { Topic = "EU AI Act" })  // any IAgentLlm
    .AddTool<GetWeatherTool>()                    // tools off by default — add what this call needs
    .AddTool<SaveNoteTool>()
    .AddMcp("github", "https://mcp.example.com/sse", token)
    .RunAsync();

Console.WriteLine($"Answer: {result.Value}");
Console.WriteLine($"Iterations: {result.Iterations}, cost: {result.Total.Cost}");
```

### Authoring a tool

Tools are typed exactly like prompts. Inherit `ToolBase<TInput, TOutput>` and the library
generates the input schema, deserializes the model's arguments, serializes your result, and traps
exceptions.

```csharp
public sealed class SaveNoteTool(INoteStore store)
    : ToolBase<SaveNoteTool.Input, SaveNoteTool.Output>
{
    public override string Name        => "save_note";
    public override string Description => "Persists a note and returns its id.";

    public override async Task<Output> ExecuteAsync(Input input, CancellationToken ct)
    {
        var id = await store.SaveAsync(input.Title, input.Body, ct);
        return new Output { Id = id };
    }

    public sealed class Input
    {
        [Description("Short title.")]      public required string Title { get; init; }
        [Description("Note body / text.")] public required string Body  { get; init; }
    }

    public sealed class Output { public Guid Id { get; init; } }
}
```

Throwing is fine. The runner catches the exception and returns the error to the model, which can
retry with different arguments or fall back, controlled by `ToolExceptionPolicy`.

### Server context a tool can trust (and the model cannot forge)

Some tools must act on data that has to come from the server — the signed-in user's id, the
tenant, a permission scope. Putting it in the tool's input is unsafe: input is part of the schema,
so the model fills it in and could send anything. Inherit `ToolBase<TScope, TInput, TOutput>`
instead — `TScope` comes **first** (trusted server data), then the model's `TInput`. The caller
supplies the value per call via `context:`; it never reaches the model, so it cannot be read or
forged through the prompt and flows untouched through the whole pipeline.

```csharp
public sealed record UserContext(Guid UserId, string UserName, Guid TenantId);

public sealed class GetMyOrdersTool(IOrderRepository orders)
    : ToolBase<UserContext, GetMyOrdersTool.Input, GetMyOrdersTool.Output>
{
    public override string Name        => "get_my_orders";
    public override string Description => "Lists the signed-in user's orders.";

    // context = trusted server data (first); input = model arguments (second).
    public override async Task<Output> ExecuteAsync(UserContext context, Input input, CancellationToken ct)
    {
        var rows = await orders.GetForUserAsync(context.UserId, input.Status, ct);
        return new Output { Count = rows.Count };
    }

    public sealed class Input  { [Description("Optional status filter.")] public string? Status { get; init; } }
    public sealed class Output { public int Count { get; init; } }
}
```

Supply the context on the builder with `.WithContext(...)` — each scoped tool resolves the value
matching its `TScope` by type:

```csharp
var user = new UserContext(currentUser.Id, currentUser.Name, currentUser.TenantId);

await ai.Agent(new GPT5(), prompt)
    .AddTool<GetMyOrdersTool>()
    .WithContext(user)                // call .WithContext again per extra scoped context type
    .RunAsync();
```

The runner guarantees `context` is non-null and correctly typed before calling, so you never
null-check a *missing* context. If a scoped tool runs but no matching value was supplied, the
runner throws `AiToolContextException` to **you** (a wiring mistake caught at first run), never to
the model. Validate the context's *contents* (permissions, etc.) inside the tool as usual.

### Registering tools and MCP servers

Add tools on the builder (above), or register global defaults. Globally registered defaults are
**opt-in** — a call gets them only when it asks, with `.AddDefaultTools()` / `.AddDefaultMcp()`.
This keeps a tool registered for one flow from silently leaking into every other agent call.

```csharp
builder.Services.AddAiTools<SaveNoteTool>();          // type, resolved from DI
builder.Services.AddAiTools(new ReportBugTool());     // pre-built instance
builder.Services.AddAiMcp(new Mcp("github", "https://mcp.example.com/sse", token));

// opt a specific call into the registered set:
await ai.Agent(new GPT5(), prompt).AddDefaultTools().AddDefaultMcp().RunAsync();
```

### External MCP (client only)

`Mcp` describes a remote Model Context Protocol server reached over HTTPS/SSE. Its tools are
presented to the model as `"{server}.{tool}"` (for example `github.read_file`); `AllowedTools`
whitelists which ones are exposed.

```csharp
var mcp = new Mcp(
    name:  "github",
    url:   "https://mcp.example.com/sse",
    token: bearer,                                  // optional Authorization: Bearer
    allowedTools: ["read_file", "create_issue"]);   // optional whitelist
```

### Per-call options

Every knob is a chainable builder method:

```csharp
await ai.Agent(new GPT5(), prompt)
    .AddTool<SaveNoteTool>()
    .MaxIterations(12)
    .MaxParallelToolCalls(8)
    .Timeout(TimeSpan.FromMinutes(2))
    .AllowOnly("save_note", "github.read_file")
    .OnToolCall(async (call, ct) => call.Name != "delete_everything")   // veto hook
    .RunAsync();
```

| Method | Purpose |
| :--- | :--- |
| `.MaxIterations(n)` | Hard ceiling on agent turns |
| `.MaxParallelToolCalls(n)` | Concurrency for tool execution within a turn (surplus is queued, never dropped) |
| `.Timeout(t)` | Wall-clock limit for the whole run |
| `.AllowOnly(names…)` | Per-call allow-list of tool names |
| `.OnToolCall((call, ct) => …)` | Async hook before each tool; return `false` to block it |
| `.AddDefaultTools()` / `.AddDefaultMcp()` | **Opt IN** to DI-registered defaults (off otherwise) |
| `.MaxNestedDepth(n)` | Bound on agent-to-tool-to-agent nesting |

### The audit trail: `ResultAgent<T>`

`ResultAgent<T>` extends `Result<T>` with everything that happened during the run, for logging,
debugging, replay or cross-model verification:

```csharp
public class ResultAgent<T> : Result<T>
{
    public int                          Iterations    { get; }  // model round-trips
    public IReadOnlyList<ToolInvocation> ToolCalls     { get; }  // ordered, with timings
    public AiUsageSummary               Request       { get; }  // this agent's own turns
    public AiUsageSummary               Total         { get; }  // whole run, including nested AI
    public AiUsageScope?                Usage         { get; }  // full call tree
    public IReadOnlyList<AiUsageScope>  NestedAiCalls { get; }  // flat list of nested calls
}

foreach (var call in result.ToolCalls)
    Console.WriteLine($"[{call.Iteration}] {call.Name}: {call.Duration.TotalMilliseconds:F0} ms"
                    + (call.IsError ? $" failed: {call.Error}" : ""));
```

Each `ToolInvocation` records the iteration, name, input, output, error and error type, duration,
originating MCP server, whether it was blocked, and any nested AI usage the tool itself consumed.

### Nested cost tracking

If a tool injects `IAiProvider` and calls another model, that cost is tracked too. This is the
difference between `Request` and `Total`:

- `Request`: only the main agent's own model turns.
- `Total`: the entire run, including every nested model call made inside tools and sub-agents.
  This is your end-to-end number for billing or quota.

`NestedAiCalls` gives the flat list (model, tokens, cost, duration, originating tool) and `Usage`
the full tree. Tracking flows with the async context, so concurrent requests stay isolated with
no plumbing on your side.

### Streaming an agent run

`.RunStreamAsync()` emits a sealed `AgentEvent` hierarchy so you can drive a live UI:

```csharp
await foreach (var evt in ai.Agent(new Sonnet46(), prompt).AddTool<SaveNoteTool>().RunStreamAsync())
{
    switch (evt)
    {
        case AgentIterationStartedEvent e:   ui.NewTurn(e.Iteration);                break;
        case AgentToolCallStartedEvent s:    ui.ShowTool(s.ToolName, s.CallId);      break;
        case AgentToolCallCompletedEvent d:  ui.MarkDone(d.Invocation);              break;
        case AgentFinalTextEvent f:          ui.AppendFinal(f.Text);                 break;
        case AgentCompletedEvent<MyAnswer> c: ui.Done(c.Result);                     break;
        case AgentFailedEvent x:             ui.Error(x.Error);                       break;
    }
}
```

`ai.Chat(llm, prompt, history).RunStreamAsync()` does the same resuming from a prior transcript
with full tool-calling (needs an `IAgentLlm` model).

### Chat and agent API at a glance

| Call | Returns | Tools | Streaming |
| :--- | :--- | :---: | :---: |
| `ChatAsync(llm, prompt, history)` | `Result<T>` | no | no |
| `ChatStreamAsync(llm, prompt, history)` | `IAsyncEnumerable<string>` | no | tokens |
| `Agent(agentLlm, prompt).RunAsync()` | `ResultAgent<T>` | yes | no |
| `Agent(agentLlm, prompt).RunStreamAsync()` | `IAsyncEnumerable<AgentEvent>` | yes | events |
| `Chat(llm, prompt, history).RunAsync()` | `Result<T>` | yes | no |
| `Chat(llm, prompt, history).RunStreamAsync()` | `IAsyncEnumerable<AgentEvent>` | yes | events |

Every entry point has a plain-`string` overload for when you do not need a typed response.

---

## Ready-made prompts

`Zonit.Extensions.Ai.Prompts` is a separate package of reusable, production-grade prompt
templates. When the task matches one, install the package and use it instead of writing a prompt
from scratch.

```bash
dotnet add package Zonit.Extensions.Ai.Prompts
```

`TranslatePrompt` translates text as a native writer would, applying per-language localization
rules (quotation marks, number and date formats, dash conventions and register) for 19 languages
(`en, pl, de, es, fr, it, pt, nl, sv, da, no, fi, ru, uk, cs, sk, hu, tr, ar`), with a general
fallback for any other target.

```csharp
using Zonit.Extensions.Ai.Prompts;

var result = await ai.GenerateAsync(
    new GPT5(),
    new TranslatePrompt { Content = "Hello world!", Target = "pl" });

string translated = result.Value;   // "Witaj świecie!" — translated text as a plain string
```

More templates will be added over time. Details in [`Instruction/prompt-library.md`](./Instruction/prompt-library.md).

---

## Cost tracking and estimation

Read actual cost from any result:

```csharp
var r = await ai.GenerateAsync(new GPT5(), prompt);
Console.WriteLine($"{r.MetaData.InputTokens} in / {r.MetaData.OutputTokens} out");
Console.WriteLine($"Total: {r.MetaData.TotalCost}");
```

Or estimate before you call:

```csharp
Price perCall   = ai.CalculateCost(new GPT5(), inputTokens: 1_000, outputTokens: 500);
Price embedCost = ai.CalculateCost(new TextEmbedding3Large(), inputTokens: 1_000);
Price imageCost = ai.CalculateCost(new GPTImage15 { Quality = GPTImage15.QualityType.High,
                                                    Size = GPTImage15.SizeType.Square });
Price estimate  = ai.EstimateCost(new GPT5(), "your prompt text...", estimatedOutputTokens: 500);
```

---

## Configuration

### Hierarchy

Everything lives under the `Ai` section: shared settings such as `Resilience` at the top, one
subsection per provider.

```json
{
  "Ai": {
    "Resilience": { "MaxRetryAttempts": 3, "AttemptTimeout": "00:10:00" },
    "OpenAi":     { "ApiKey": "sk-...", "OrganizationId": "org-...", "Timeout": "00:15:00" },
    "Anthropic":  { "ApiKey": "sk-ant-..." },
    "Agent":      { "MaxIterations": 100, "MaxParallelToolCalls": 16 }
  }
}
```

### Code-based overrides

`appsettings.json` is bound first; code overrides are applied on top through `PostConfigure`, so
you set only what you need:

```csharp
// API key inline
builder.Services.AddAiOpenAi("sk-...");

// Or full options (for example an Azure OpenAI endpoint)
builder.Services.AddAiOpenAi(o =>
{
    o.OrganizationId = "org-...";
    o.BaseUrl        = "https://my-azure-openai.openai.azure.com";
    o.Timeout        = TimeSpan.FromMinutes(15);   // per-provider override
});
```

Keep secrets in User Secrets, environment variables or a vault; do not hard-code keys in source.
Each provider extends a common `AiProviderOptions` (`ApiKey`, `BaseUrl`, `Timeout`) and adds its
own fields where relevant.

---

## Resilience

Built on `Microsoft.Extensions.Http.Resilience`, tuned for long-running AI requests. Retry,
timeout and circuit-breaker apply to every provider and are configurable under `Ai:Resilience`
(or in code via `AddAi(o => o.Resilience...)`).

| Setting | Default | Meaning |
| :--- | :--- | :--- |
| `TotalRequestTimeout` | 40 min | Budget for the whole pipeline, including retries |
| `AttemptTimeout` | 10 min | Timeout for a single attempt |
| `MaxRetryAttempts` | 3 | Retries on transient failures |
| `RetryBaseDelay` / `RetryMaxDelay` | 2 s / 30 s | Exponential backoff bounds |
| `UseJitter` | `true` | Randomises delays to avoid thundering herds |
| `CircuitBreakerFailureRatio` | 0.5 | Failure ratio that opens the circuit |

Requests are retried on network errors, timeouts, HTTP 429, and 5xx responses.

---

## AOT and trimming

The library targets `net10.0` and is built for Native AOT and trimming. For the documented
typed-prompt pattern the structured-output path is fully source-generated and runs zero
reflection: incremental generators emit, at build time, the request JSON Schema and the
`JsonTypeInfo<T>` for `T` and its whole graph, plus the Scriban property binding. Each provider
also serializes its wire DTOs through its own source-generated `JsonSerializerContext`.

| Path | AOT / trim |
| :--- | :--- |
| Text · streaming · embeddings · images · transcription | ✅ source-generated, zero reflection |
| Structured output via `PromptBase<T>` | ✅ schema and `JsonTypeInfo<T>` generated at build time |
| Scriban prompt rendering | ✅ source-generated property binding |
| Tools · agents · MCP | ⚠️ require dynamic code; tool arguments are (de)serialized reflectively, and these APIs are annotated `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` |
| Ad-hoc response types the generator never sees (for example `SimplePrompt<T>` over a type shaped at runtime) | ⚠️ reflection fallback; not AOT-safe |

The only reflection on the structured-output path is a gated fallback for response types the
source generator did not see; for the typed-prompt pattern it never runs, so the trim and AOT
analyzers stay clean without suppressing real warnings.

To publish Native AOT, set `<PublishAot>true</PublishAot>` in your app and:

```bash
dotnet publish -c Release -r win-x64
```

---

## Provider packages

Install only what you use. Every package version and download count below is live from NuGet.

### Core

| Package | Version | Downloads |
| :--- | :--- | :--- |
| **Zonit.Extensions.Ai** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.svg?label=) |
| **Zonit.Extensions.Ai.Abstractions** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Abstractions.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Abstractions) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Abstractions.svg?label=) |
| **Zonit.Extensions.Ai.Prompts** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Prompts.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Prompts) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Prompts.svg?label=) |

### Providers

| Provider | Package | Version | Downloads |
| :--- | :--- | :--- | :--- |
| OpenAI | **Zonit.Extensions.Ai.OpenAi** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.OpenAi.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.OpenAi) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.OpenAi.svg?label=) |
| Anthropic | **Zonit.Extensions.Ai.Anthropic** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Anthropic.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Anthropic) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Anthropic.svg?label=) |
| Google | **Zonit.Extensions.Ai.Google** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Google.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Google) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Google.svg?label=) |
| xAI | **Zonit.Extensions.Ai.X** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.X.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.X) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.X.svg?label=) |
| DeepSeek | **Zonit.Extensions.Ai.DeepSeek** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.DeepSeek.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.DeepSeek) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.DeepSeek.svg?label=) |
| Mistral | **Zonit.Extensions.Ai.Mistral** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Mistral.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Mistral) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Mistral.svg?label=) |
| Groq | **Zonit.Extensions.Ai.Groq** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Groq.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Groq) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Groq.svg?label=) |
| Together AI | **Zonit.Extensions.Ai.Together** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Together.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Together) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Together.svg?label=) |
| Fireworks | **Zonit.Extensions.Ai.Fireworks** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Fireworks.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Fireworks) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Fireworks.svg?label=) |
| Cohere | **Zonit.Extensions.Ai.Cohere** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Cohere.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Cohere) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Cohere.svg?label=) |
| Perplexity | **Zonit.Extensions.Ai.Perplexity** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Perplexity.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Perplexity) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Perplexity.svg?label=) |
| Alibaba | **Zonit.Extensions.Ai.Alibaba** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Alibaba.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Alibaba) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Alibaba.svg?label=) |
| Baidu | **Zonit.Extensions.Ai.Baidu** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Baidu.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Baidu) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Baidu.svg?label=) |
| Zhipu | **Zonit.Extensions.Ai.Zhipu** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Zhipu.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Zhipu) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Zhipu.svg?label=) |
| Moonshot | **Zonit.Extensions.Ai.Moonshot** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Moonshot.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Moonshot) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Moonshot.svg?label=) |
| 01.AI | **Zonit.Extensions.Ai.Yi** | [![v](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Yi.svg?label=)](https://www.nuget.org/packages/Zonit.Extensions.Ai.Yi) | ![dt](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Yi.svg?label=) |

Each provider is registered with its matching extension method: `AddAiOpenAi()`,
`AddAiAnthropic()`, `AddAiGoogle()`, `AddAiX()`, `AddAiGroq()`, and so on.

---

## Architecture

The library follows Clean Architecture and SOLID principles:

- **Abstractions-only domain.** `Zonit.Extensions.Ai.Abstractions` holds every interface and
  contract (`IAiProvider`, `ILlm`, `IPrompt`, `ITool`, results, agent events). Your domain layer
  can define prompts and tools referencing only this package, with no HTTP, DI or Scriban.
- **Self-contained providers.** Each provider is an independent package with its own options, DI
  extension, model classes and `JsonSerializerContext`. Adding a provider never touches the core
  (Open/Closed).
- **Idempotent registration.** `AddAi*` methods use `TryAdd*` and `TryAddEnumerable`, so they are
  safe to call from multiple modules or plugins.
- **The core owns the agent loop.** Providers implement a thin `IAgentProviderAdapter`; iteration,
  parallel tool execution, MCP merging and audit assembly live in the core.

```
Zonit.Extensions.Ai.Abstractions    interfaces and contracts (domain-safe)
Zonit.Extensions.Ai                 orchestrator, agent runtime, MCP client, templating, DI
Zonit.Extensions.Ai.SourceGenerators  registration, schema and binding generators
Zonit.Extensions.Ai.{Provider}      one package per provider
Zonit.Extensions.Ai.Prompts         optional ready-made prompts
```

---

## Requirements and license

- **.NET 10.0** (single target framework).
- Released under the **MIT License**. See [LICENSE.txt](./LICENSE.txt).

Issues and pull requests are welcome at
**[github.com/Zonit/Zonit.Extensions.Ai](https://github.com/Zonit/Zonit.Extensions.Ai)**.

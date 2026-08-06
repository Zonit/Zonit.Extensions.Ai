# Patch notes

Dated, version-scoped change log. The other guides describe the library as it is *now*; this file
records *what changed and why*.

## Unreleased

### GPT-5.6 price cut, and long-context pricing keyed on the context size

**Breaking — `ILlm` gained methods and `GetOutputPrice` gained a parameter.** Only code that
*implements* `ILlm` by hand is affected; callers of `IAiProvider.CalculateCost` are not.

- **Changed** GPT-5.6 pricing to OpenAI's 2026-07-30 cut: `Terra56` $2.50/$15 → **$2/$12** (cached
  $0.25 → $0.20, batch $1.25/$7.50 → $1/$6) and `Luna56` $1/$6 → **$0.20/$1.20** (cached $0.10 →
  $0.02, batch $0.50/$3 → $0.10/$0.60). `Sol56` is unchanged at $5/$30.
- **Changed** `ILlm.GetOutputPrice(long tokenCount)` to `GetOutputPrice(long inputTokens, long
  outputTokens)`. Long-context surcharges are keyed on the size of the **context**, even the ones
  that raise the *output* rate — GPT-5.6 bills output at 1.5× ($45 / $18 / $1.80) once input passes
  272K. The old signature only saw the output count, so that tier could never be reached.
- **Fixed** the same dead threshold in five xAI models (`Grok420Reasoning`, `Grok420NonReasoning`,
  `Grok420MultiAgent`, `Grok41FastReasoning`, `Grok41FastNonReasoning`): they compared the *output*
  count against 200K / 128K / 512K, above `MaxOutputTokens`, so the doubled and quadrupled output
  rates were unreachable.
- **Fixed** cache pricing being ignored for every reasoning model. `AiCostCalculator` gated its
  cache-aware path on `ITextLlm`, but `IReasoningLlm` declares its own `PriceCachedInput` without
  deriving from `ITextLlm` — so cache reads on the whole GPT-5.x / o3 / Sol / Terra / Luna family
  were billed at the **full input rate**. Cache rates now resolve through `ILlm.GetCachedInputPrice`
  / `GetCachedInputWritePrice`, with no marker-interface gate.
- **Added** `ILlm.GetBatchInputPrice` / `GetBatchOutputPrice`, so `CalculateBatchCost` picks up
  long-context batch tiers (Sol: $5 / $22.50 past 272K) instead of the flat `BatchPrice*`
  properties.
- **Changed** `AiCostCalculator.CalculateOutputCost(llm, outputTokens)` to
  `CalculateOutputCost(llm, inputTokens, outputTokens)`.
- **Unchanged** for model authors who do not tier by context: `LlmBase` supplies every new method
  with the classic flat-rate behaviour, so only models that genuinely price differently above a
  threshold override anything.

### Anthropic single-shot calls stream on the wire and reassemble

**Not breaking — no API change.** `GenerateAsync` / `ChatAsync` still take the same arguments and
still return one finished `Result<T>`. What changed is underneath: the Anthropic HTTP transport now
sends `stream: true` and rebuilds the complete response from the SSE frames.

- **Fixed** a hang on large responses. The previous buffered `POST` held one HTTP response open for
  the entire generation. Because `MaxTokens` defaults to the model's full output capacity (128k on
  Opus / Sonnet 5), a large structured answer could legitimately outlive the per-attempt timeout —
  at which point the request was cancelled and **retried from zero**, multiplying cost with no
  chance of succeeding. Anthropic documents this and their own SDKs refuse to send such a request.
  Lowering `MaxTokens` (the historical workaround) is no longer needed.
- **Changed** the Anthropic API transport to streaming resilience. A per-attempt wall-clock cap must
  not apply to a stream; liveness is enforced by `Ai:Resilience.InterEventTimeout` and HTTP/2
  keep-alive pings instead. `AttemptTimeout` no longer affects Anthropic text calls.
- **Added** strict truncation handling. A stream that ends before the terminal `message_stop`, or
  whose tool-input fragments do not form valid JSON, now throws. A buffered response was
  all-or-nothing for free; a stream is not, and a half-parsed structured answer must never reach the
  caller as a success.
- **Unchanged** `StreamAsync` / `ChatStreamAsync` — still live text deltas, still text-only. Output
  size is not a reason to choose them over `GenerateAsync`; see [`usage.md`](./usage.md) → *Three
  ways to run a call*.

## 10.6.2 — 2026-07-26

### Claude Opus 5; batch TTS with stitching, seeds and subtitle alignment

- **Added** `Opus5` (`claude-opus-5`) — successor to `Opus48`, same pricing ($5 / $25 per MTok,
  cache write $6.25, read $0.50), 1M context / 128K output, fast mode ($10 / $50) via `IFast`, full
  `Low`…`Max` effort ladder including `Extra` (wire `xhigh`). Thinking is **on by default on the
  wire**, so the class sets `ThinkingEnabledByDefault` like `Sonnet5`: leaving `Reason` unset still
  means "no thinking" because the provider then sends an explicit `thinking: {"type":"disabled"}`.
- **Deprecated** `Opus48` → migrate to `Opus5`.
- **Added** TTS overloads on `IAiProvider` / `IModelProvider`: `SpeechPrompt { Text, PreviousText?,
  NextText? }` (keeps prosody continuous between lines), batch synthesis from
  `IReadOnlyList<string>` with automatic stitching of neighbouring sentences, and
  `GenerateWithTimestampsAsync` → `Result<SpeechTimestamps>` with per-character timing
  (`.ToWords()` for subtitle cues).
- **Added** ElevenLabs request options: seed, speed, language, text normalization and audio tags;
  implemented the `/with-timestamps` endpoints and alignment mapping, plus request validation.

## 10.6.1 — 2026-07-22

### Superseded models marked `[Obsolete]`

- **Deprecated** model classes that newer releases replaced, across Google (Gemini), OpenAI (GPT,
  image) and xAI (Grok), each carrying its recommended replacement in the attribute message.
  Behaviour is unchanged — the models still work; the attribute only signals what not to pick for
  new code.
- **Changed** `llms.md` to flag deprecated classes with ⚠️ and list the replacement in the
  *Deprecated models* section.

## 10.6.0 — 2026-07-22

### Typed image/video prompts and central input validation

**Breaking.** The image and video `GenerateAsync` overloads no longer accept a plain `IPrompt<Asset>` —
they take `IImagePrompt` / `IVideoPrompt`, so a text prompt can no longer be passed to an image model
by accident.

- **Added** `IImagePrompt` / `IVideoPrompt` and the ready-made `ImagePrompt` / `VideoPrompt` classes
  (`Text` plus an optional source `Image` / `Video`). For templated prompts, inherit
  `ImagePromptBase` / `VideoPromptBase`.
- **Added** central input validation: files are checked against the model's declared channels, and a
  call must carry text or a file. Mismatches now fail fast with a clear message instead of reaching
  the provider.
- **Added** `GrokImagineVideo15` (`grok-imagine-video-1.5`).

#### Migration

```csharp
// Before
await ai.GenerateAsync(imageModel, new MyPrompt { … }, ct);

// After
await ai.GenerateAsync(imageModel, new ImagePrompt { Text = "a red bicycle" }, ct);
await ai.GenerateAsync(imageModel, new ImagePrompt { Text = "make it snowy", Image = source }, ct);
await ai.GenerateAsync(videoModel, new VideoPrompt { Text = "slow zoom in", Image = photo }, ct);
```

## 10.5.0 — 2026-07-19

### ElevenLabs provider: text-to-speech (`ISpeechLlm`)

- **Added** the `Zonit.Extensions.Ai.ElevenLabs` package with DI registration
  (`AddAiElevenLabs()`, binds `Ai:ElevenLabs`) and the `ISpeechLlm` capability interface, so
  `GenerateAsync(speechLlm, string)` returns `Result<Asset>` with the audio.
- **Added** TTS models (`ElevenV3`, `ElevenMultilingualV2`, `ElevenTurboV2` / `V2_5`,
  `ElevenFlashV2` / `V2_5`), the `ElevenVoices` catalogue, `ElevenAudioFormat`, and per-character
  billing so cost lands in `MetaData` like every other modality.

## 10.4.0 — 2026-07-12

### Global HTTP/SOCKS proxy for every provider

- **Added** the `Ai:Proxy` section (`AiProxyOptions`: `Enabled`, `Address`, `Username`, `Password`)
  — set once, applied to every provider's HTTP client. Supports HTTP and SOCKS addresses.
- **Added** a per-provider opt-out: `AiProviderOptions.UseProxy = false` excludes one provider while
  the rest keep the proxy. Useful for reaching a region-locked model (e.g. Grok 4.5 is EU-blocked)
  through an allowed-region exit node without routing unrelated traffic.

## 10.3.7 — 2026-07-12

### Fixed: OpenAI agent stopped after one tool call

- **Fixed** `OpenAiAgentSession` dropping per-request configuration on continuation. Tools,
  instructions, output format and reasoning are now resent with **every** request that carries a
  `previous_response_id` — previously they were sent only on the first turn, so the model lost its
  tools mid-run and the agent behaved as if it had none.

## 10.3.6 — 2026-07-12

### Strongly-typed reasoning effort for OpenAI models

**Breaking.** The per-model nested `ReasonType` enum is gone. Effort levels are now enforced by the
model's own enum, so passing a level a model does not support is a compile-time error.

- **Removed** `ReasonType` from OpenAI model classes.
- **Added** `OpenAiReasoningBase<TReason>` plus `OpenAiReasonEffort` (`None`, `Low`, `Medium`,
  `High` — GPT-5.0–5.5 and the o-series) and `OpenAiReasonEffortExtended`, which adds `Xhigh` for
  GPT-5.6 (Sol / Terra / Luna).

#### Migration

```csharp
// Before
new GPT55 { Reason = GPT55.ReasonType.High }

// After
new GPT55 { Reason = OpenAiReasonEffort.High }
new Sol56 { Reason = OpenAiReasonEffortExtended.Xhigh }
```

## 10.3.5 — 2026-07-11

### OpenAI GPT-5.6 tier: Sol / Terra / Luna

- **Added** `Sol56` (`gpt-5.6-sol`), `Terra56` (`gpt-5.6-terra`) and `Luna56` (`gpt-5.6-luna`) —
  1,050,000 context / 128,000 output, text + image in. GPT-5.6 replaces the pro/mini/nano naming
  with these three tiers.

## 10.3.4 — 2026-07-09

### xAI Grok 4.5

- **Added** `Grok45` (`grok-4.5`) and extended `reasoning.effort` support through `XProvider` and
  `XAgentSession`.
- **Note** the model is region-locked: xAI answers `403 … not available in your region` based on the
  **source IP**, not the account. Route that provider through an allowed-region proxy — see the
  `Ai:Proxy` support added in 10.4.0.

## 10.3.3 — 2026-06-30

### Claude Sonnet 5

- **Added** `Sonnet5` (`claude-sonnet-5`) — adaptive thinking, 1M context, 128K output (Sonnet 4.6
  capped at 64K), and `Reason = Sonnet5.ReasonType.Extra` (wire `xhigh`), which Sonnet 4.6 does not
  expose.
- **Added** `AnthropicAdaptiveBase.ThinkingEnabledByDefault`. Sonnet 5 turns thinking **on** when the
  `thinking` field is omitted — unlike every other model here. With the flag set, the provider sends
  an explicit `thinking: {"type":"disabled"}` when `Reason` is unset, so "Reason not set" keeps
  meaning "no thinking" across the whole SDK.
- **Deprecated** `Sonnet46` → migrate to `Sonnet5`.

## 10.3.2 — 2026-06-30

### Recovery from double-encoded JSON

- **Fixed** `JsonResponseParser` failing when a model returned JSON as a *string* inside the JSON
  (arrays and objects alike, including a whole collection stuffed into one property). Recovery runs
  **only** after a deserialization error, so valid responses take the same path as before.

## 10.3.1 — 2026-06-21

### `ConversationInfo` in the run context

- **Added** `ConversationInfo` to `IRunContext` (`MessageCount`, `IsEmpty`), initialized by
  `AgentRunner` before tools are resolved. Lets a tool or a sub-agent's `IsAvailable` branch on how
  far the conversation has got — e.g. expose an onboarding tool only on an empty conversation.

## 10.3.0 — 2026-06-20

### Tool context is now a typed bag (`IRunContext`); sub-agents can hide themselves (`IsAvailable`)

**Breaking.** A tool's trusted server context moved from a single typed `TScope` to a typed **bag**
passed as the first parameter of `ExecuteAsync`. This lets one tool read many context models — and
*write* back into them — instead of being limited to one overloaded context object.

- **Removed** `ToolBase<TScope, TInput, TOutput>` (the three-generic scoped base) and the internal
  `IScopedTool`. `ToolBase` is back to two generics: `ToolBase<TInput, TOutput>`.
- **Changed** the tool entry point to take the run context first:
  `ExecuteAsync(IRunContext context, TInput input, CancellationToken ct)`. **Every** tool override
  gains the `IRunContext context` first parameter (tools that need no context simply ignore it).
- **Added** `IRunContext` (+ `RunContext`, public, in `Zonit.Extensions.Ai.Abstractions`; reflection-free,
  AOT/trim-clean). A type-keyed bag mirroring ASP.NET Core's `IFeatureCollection`:
  - `Get<T>()` → value or `null`; `GetRequired<T>()` → value or throws `AiToolContextException` **to
    the caller** (a wiring mistake, never reported to the model); `TryGet<T>(out T?)`; `Has<T>()`;
    `Set<T>(value)`; `Values`.
  - The bag holds your instances **by reference**, so a tool can write a server-resolved value into a
    context model (e.g. stamp a worker id) instead of returning it through the model — keeping it out
    of the token stream where the model could alter it. Mutability follows the model's own accessors
    (`set` vs `init`). Backed by a `ConcurrentDictionary` (structurally safe under parallel tool calls;
    making the held models thread-safe is yours to decide).
- **Added** `IAgent.IsAvailable(IRunContext context)` (default `true`, overridable on `AgentBase`).
  Return `false` and the sub-agent is omitted from the parent model's tool set — declarative
  permission / scenario gating. Evaluated **once** when the run's tool set is assembled (a sub-agent
  can't be removed mid-run); the next run re-evaluates against a refreshed context. Keep it
  synchronous and side-effect-free — load permission data into the context *before* the run.
- **Unchanged** `.WithContext(...)` on the fluent builder still seeds the values (call once per
  distinct type); the runner now builds one `IRunContext` per run, shares it across every tool, and
  forwards it to sub-agents.

#### Migration

```csharp
// Before (≤ 10.2.x) — single TScope, resolved by type into the first parameter:
public sealed class GetMyOrdersTool : ToolBase<UserContext, Input, Output>
{
    public override Task<Output> ExecuteAsync(UserContext context, Input input, CancellationToken ct)
    {
        var userId = context.UserId;
        ...
    }
}

// After (11.0.0) — two generics; read what you need from the bag:
public sealed class GetMyOrdersTool : ToolBase<Input, Output>
{
    public override Task<Output> ExecuteAsync(IRunContext context, Input input, CancellationToken ct)
    {
        var user = context.GetRequired<UserContext>();   // same throw-if-missing guarantee
        var userId = user.UserId;
        ...
    }
}
```

A plain tool that read no context simply adds the unused first parameter:
`ExecuteAsync(Input input, …)` → `ExecuteAsync(IRunContext context, Input input, …)`. `.WithContext(...)`
call sites are unchanged. See [`tools.md`](./tools.md) and [`subagents.md`](./subagents.md).

#### Why

The single-`TScope` model forced one overloaded context object and capped a tool at exactly one
context type — a tool needing data from two models couldn't get it, and the object grew bloated. The
bag lets each tool pull only the models it cares about, register as many as needed, and write
server-resolved values back so sensitive ids never round-trip through the model. `IsAvailable` builds
permissions, plans and scenarios straight into agent assembly instead of leaving the model to police
itself.

## 10.2.2 — 2026-06-19

### Provider fixes: MCP config on Windows, Gemini tool results, OpenAI/xAI structured output

- **Fixed** the Anthropic CLI transport passing MCP configuration inline, which `cmd.exe` mangled on
  Windows. It is now written to a temporary `.json` file and deleted after the run.
- **Fixed** Google / Gemini rejecting a tool result that was not a JSON object. A scalar or array
  return value is now wrapped as `{ "result": … }` before being sent as `functionResponse.response`.
- **Fixed** OpenAI and xAI structured output being sent as `response_format`. Both now use
  `text.format`, which is what those APIs require — and it is resent on every turn, verified by a
  regression test.
- **Added** LIVE smoke tests for OpenAI, Google, Anthropic (API and SDK) and xAI agents covering
  tool use, structured output and loose schemas.

## 10.2.1 — 2026-06-17

### Trusted tool context over the CLI / MCP bridge

- **Added** `AgentSessionContext.Context` and `AgentToolContextBinder`, so a tool exposed through the
  Claude Code CLI / MCP bridge receives the same trusted context as one running on the HTTP path.
  Before this, `.WithContext(...)` values reached tools only on the direct provider path.

## 10.2.0 — 2026-06-17

### Sub-agents: their own MCP servers, and an unbounded tool builder

- **Added** `IAgent.Mcps` (`IReadOnlyList<Mcp>`, empty by default, defaulted on both `AgentBase<TOutput>`
  and `AgentBase<TInput, TOutput>`). A sub-agent can now declare its **own** MCP servers alongside its
  own `Tools` — `public override IReadOnlyList<Mcp> Mcps => [new("github", "https://…/sse", token, …)];`.
  When the parent delegates, those servers are connected for the sub-agent's nested run and their remote
  tools are exposed under the `"{Name}.{tool}"` prefix (filtered by `Mcp.AllowedTools`: `null` = all,
  empty = none). The parent's MCP servers are **not** inherited — a sub-agent only sees what it declares,
  the same rule already used for `Tools`. See [`subagents.md`](./subagents.md).
- **Added** `Toolset.Add<T>()` → `ToolsetBuilder`, a `typeof`-free, compile-checked, **unbounded** tool
  chain: `Toolset.Add<A>().Add<B>().Add<C>()…`. The fixed-arity `Toolset.Of<…>()` overloads (one to six)
  capped a sub-agent at six tools; `Add<T>()` removes that ceiling. The builder implements
  `IReadOnlyList<Type>`, so it drops straight into `IAgent.Tools`; each `Add` returns a new immutable
  builder (no shared-state surprises). `Of<…>()` and `Toolset.None` are unchanged.

#### Why

Sub-agents could already carry their own local tools but had no way to reach an external MCP server, and
the `Toolset.Of<…>()` helper stopped at six tools. Both are now first-class: per-agent MCP and an
arbitrary number of tools, with no `typeof` and no reflection (AOT/trim-clean).

## 10.1.0 — 2026-06-17

### Anthropic: Claude Code CLI transport, and tool-using agents over it

- **Added** `AnthropicOptions.Transport` (`AnthropicTransport` enum): `Api` (default, HTTP Messages
  API — unchanged behaviour), `Sdk` (run through the local **Claude Code CLI** `claude -p`, authed by
  the machine's `claude login` subscription — no API key), and `Auto` (CLI first, fall back to the
  HTTP API for what the CLI can't do when `ApiKey` is set, else throw). The transport is chosen
  **explicitly** as the first argument — `AddAiAnthropic(AnthropicTransport.Sdk, …)` — or via
  `"Ai:Anthropic:Transport"`, because the CLI is not behaviourally identical to the API (Claude Code
  applies its own system prompt). See [`sdk.md`](./sdk.md).
- **Added** `AnthropicCliOptions` (bound from `Ai:Anthropic:Cli`): `ExecutablePath` (else OS
  auto-discovery), `PermissionMode`, `OAuthToken`, `AuthToken`, `WorkingDirectory`, `Timeout`,
  `AdditionalArguments`, `AdditionalEnvironment`. On the SDK transport, prompt-cache markers are
  ignored (the CLI caches automatically); requests the CLI can't represent (image/PDF attachments)
  fall back to the API under `Auto`, or throw under `Sdk`.
- **Added** the opt-in **`Zonit.Extensions.Ai.Sdk`** package + `AddAiAgentToolBridge()`. It hosts the
  app's `ITool` set as a secured **loopback (`127.0.0.1`) MCP server** (per-run bearer token) so a
  CLI-driven agent (`claude -p`) can call your C# tools. Required for tool-using agents on `Sdk`/`Auto`;
  without it, `Auto` falls back to the HTTP API (when `ApiKey` is set) and `Sdk` throws. Hand-rolled
  (`HttpListener` + `System.Text.Json`), no ASP.NET Core, AOT/trim-clean. (The `Zonit.Extensions.Ai.Mcp.Server`
  name is reserved for a future general-purpose MCP server.) See [`sdk.md`](./sdk.md).
- **Note** — on the CLI agent path the CLI owns the loop, so framework-side gates
  (`MaxIterations`/`MaxParallelToolCalls`/`OnToolCall`/per-tool timeout) and nested-usage tracking do
  not apply; token usage comes from the CLI's report. Use the `Api` transport when you need them.

#### Why

To let requests (and tool-using agents) run through a Claude **subscription** via the Claude Code CLI
instead of a metered API key, on Windows and Linux — while keeping the HTTP API as the default and the
`Auto` fallback for anything the CLI cannot do.

## 10.0.8 — 2026-06-17

### Resilience: one shared retry model for every provider

- **Added** `Ai:Resilience.InterEventTimeout` (stream-liveness watchdog) and a shared
  `AiResilienceOptions.RetryDelay(attempt)` schedule. The client-side stream / agent-turn retry now
  reads the **same** `MaxRetryAttempts` / `RetryBaseDelay` / `RetryMaxDelay` knobs as the HTTP-layer
  Polly retries — configure retry once, it applies to both layers and every provider.
- **Changed** retry defaults to step over a typical 30–90 s provider incident:
  `MaxRetryAttempts` 3 → 6, `RetryBaseDelay` 2 s → 5 s, `RetryMaxDelay` 30 s → 60 s.
- **Removed** the provider-local stream knobs `AnthropicOptions.StreamMaxRetries`,
  `StreamRetryBaseDelay`, `StreamInterEventTimeout`. Use `Ai:Resilience` instead.

  | Old (`Ai:Anthropic`) | New (`Ai:Resilience`) |
  | :--- | :--- |
  | `StreamMaxRetries` | `MaxRetryAttempts` |
  | `StreamRetryBaseDelay` | `RetryBaseDelay` |
  | `StreamInterEventTimeout` | `InterEventTimeout` |

### Empty responses now throw instead of returning empty — on every call path

- **Added** `AiEmptyResponseException` (a plain `Exception`, **not** tied to the agent loop) and the
  `AiResponseError` codes `AI-E1001` (empty after retries), `AI-E1002` (truncated), `AI-E1003`
  (refusal). See [`errors.md`](./errors.md).
- **Changed** every call path — `GenerateAsync`, `ChatAsync`, `StreamAsync`, `ChatStreamAsync`, and
  the agent loop — across **all** providers to throw a coded `AiEmptyResponseException` when the
  model yields no usable content, rather than surfacing an empty `Value` (or, on the single-shot
  path, an untyped `InvalidOperationException` — the old `"No text in … response"`). A server-side
  empty response can happen anywhere, so the type and codes are uniform everywhere. Anthropic and X
  classify the cause (truncated / refusal / data-loss) and retry the data-loss case on the shared
  budget; the OpenAI-compatible providers throw `EmptyAfterRetries` directly. Callers no longer need
  an `if (string.IsNullOrWhiteSpace(result.Value))` guard.
- `stop_reason=pause_turn` on a non-agent call still throws `InvalidOperationException` — it is a
  misuse (only the agent path resumes server-tool continuations), not an empty-content fault.

#### Why

A scheduled publish was lost during a ~35-minute provider incident: the previous defaults put all
retry attempts inside the same bad window, the turn stayed empty, and the agent returned an empty
value that flowed downstream. The longer shared budget rides out the common short blips; when an
outage outlasts it, the operation fails loudly (and is re-runnable) instead of emitting nothing.

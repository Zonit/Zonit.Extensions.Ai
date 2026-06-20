# Patch notes

Dated, version-scoped change log. The other guides describe the library as it is *now*; this file
records *what changed and why*.

## 10.2.2 — 2026-06-20

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

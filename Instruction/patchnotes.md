# Patch notes

Dated, version-scoped change log. The other guides describe the library as it is *now*; this file
records *what changed and why*.

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

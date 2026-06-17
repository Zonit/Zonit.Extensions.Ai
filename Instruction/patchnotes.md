# Patch notes

Dated, version-scoped change log. The other guides describe the library as it is *now*; this file
records *what changed and why*.

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

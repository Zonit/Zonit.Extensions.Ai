# Errors and error codes

The library signals failure with **exceptions**, never with an empty or `null` result. A model
always answers unless something technical went wrong, so "no usable content" is treated as a fault
and thrown. Practical consequence:

- A returned `Result<T>` / `ResultAgent<T>` **always** has a real `Value`. No `if (Value is null)` /
  `if (string.IsNullOrWhiteSpace(Value))` guards.
- If a call could not produce a value, control never reaches the next line — you cannot accidentally
  persist or publish an empty artifact.

## Exceptions

`AiEmptyResponseException` is raised by **every** call path — `GenerateAsync`, `ChatAsync`, the
streaming variants (`StreamAsync` / `ChatStreamAsync`), and the agent loop's `RunAsync` — because a
server-side empty/data-loss response can happen anywhere, not just inside an agent. It is a plain
`Exception` (not tied to the agent loop). The streaming variants throw it after the stream completes
having emitted nothing.

| Exception | When | Recoverable? |
| :--- | :--- | :--- |
| `AiEmptyResponseException` | Any call finished with no usable content. Carries `Code` (`AiResponseError`), `StopReason`, `Attempts`. | Depends on `Code` — see below |
| `AgentIterationLimitException` | `.MaxIterations(n)` exceeded (agent only). | Raise the cap, or fix a tool that loops |
| `AiNestingLimitException` | agent → tool → agent nesting past `MaxNestedDepth` (agent only). | Check for unintended recursion |
| `AgentException` | Agent-only base type: a tool threw to the caller, a timeout, or a final-answer parse failure. Inspect `.Partial`. | Case-by-case |
| `InvalidOperationException` | `stop_reason=pause_turn` on a non-agent call (a server-tool continuation that only the agent path can resume). | Use the agent path (`IAgentLlm`) |
| `OperationCanceledException` | The caller's `CancellationToken` was cancelled (honoured immediately). | — |

## `AiResponseError` codes

`AiEmptyResponseException.Code` classifies *why* the response was empty. The numeric value is stable
and rendered into the message as `[AI-E<code>]`, so it is greppable in logs and safe to switch on.
The codes are identical across providers and call paths.

| Code | `AiResponseError` | What happened | What to do | Transient? |
| :--- | :--- | :--- | :--- | :--- |
| **AI-E1001** | `EmptyAfterRetries` | The model ended a turn with no text and no tool call (or the stream truncated) — server-side data loss — and **every retry in the budget failed**. | Re-run the operation later; it usually succeeds. To ride out longer outages raise `Ai:Resilience` `MaxRetryAttempts`. | **Yes** — retried in-process first, then re-runnable |
| **AI-E1002** | `Truncated` | The output token budget was spent before any content (e.g. all of it on reasoning). | Raise the model's `MaxTokens` or lower the reasoning effort. Not retried — a resend re-truncates. | No |
| **AI-E1003** | `Refusal` | The model declined the input (policy / content filter). | Revise the prompt or inputs. Not retried. | No |

Only `AI-E1001` is retried automatically (on the shared schedule — see
[`configuration.md`](./configuration.md#resilience)); the deterministic faults surface at once.

## Handling

Most callers let the exception propagate — the operation failed, so the job should fail and be
retried by whatever scheduled it. Catch only when you want to branch on the cause:

```csharp
try
{
    var result = await ai.Agent(new Opus48(), new ReportPrompt())
        .AddTool(dataTool).MaxIterations(18).RunAsync(ct);

    Save(result.Value);   // Value is guaranteed present here
}
catch (AiEmptyResponseException ex) when (ex.Code == AiResponseError.EmptyAfterRetries)
{
    // Transient provider incident — skip this run; the scheduler re-runs later.
    logger.LogError(ex, "Empty after {Attempts} attempts — skipping.", ex.Attempts);
}
```

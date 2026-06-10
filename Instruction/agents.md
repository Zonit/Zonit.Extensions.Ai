# Agents (tool-calling, MCP, audit)

An agent drives the model in a loop: the model requests tool calls, the library runs them in
parallel and feeds the results back, until the model returns a final structured answer, all
behind one `await`. Build one with the fluent `ai.Agent(llm, prompt)` entry point on an
`IAgentLlm` model. For when to choose an agent over a single prompt, see [`usage.md`](./usage.md).

## Run an agent

The fluent builder is **safe by default** — nothing reaches the model unless you add it. There is
no positional overload that takes `tools` / `mcps` / `context`; that all lives on the builder. Keep
the simple `GenerateAsync(llm, prompt)` / `ChatAsync(llm, system, history)` overloads for plain
calls without tools.

```csharp
ResultAgent<Report> result = await ai.Agent(new GPT5(), prompt)   // IAgentLlm
    .AddTool<SaveNoteTool>()                  // resolved from DI (dependencies injected)
    .WithContext(user)                        // trusted server data (never sent to the model)
    .AddDefaultTools()                         // opt-in to AddAiTools<T>() defaults (off otherwise)
    .AddMcp("github", "https://mcp.example.com/sse", token,
            o => o.AllowOnly("read_file"))     // MCP wiring in its own sub-config
    .MaxIterations(12)
    .RunAsync();                               // terminal: RunAsync (await) or RunStreamAsync (events)

// Multi-turn chat carries history at the entry point:
Result<string> reply = await ai.Chat(new GPT5(), systemPrompt, history)
    .AddTool<GetVariableTool>()
    .WithContext(user)
    .RunAsync();
```

`.AddTool<T>()` is the recommended path: the container builds the tool (injecting its
dependencies) and exposes exactly it. `.AddTool(instance)` / `.AddTools(...)` take ready-made
instances for tests and scripts.

### Passing trusted server data to tools (`.WithContext(...)`)

`.WithContext(value)` delivers per-call server data to scoped tools (`ToolBase<TScope, TInput,
TOutput>`) — the current user, tenant, permission scope, etc. Values are matched to each scoped
tool's `TScope` by type and are **never** sent to the model, so the model cannot read or forge them.

```csharp
var user = new UserContext(currentUser.Id, currentUser.Name);

ResultAgent<Report> result = await ai.Agent(new GPT5(), prompt)
    .AddTool<GetMyOrdersTool>()
    .WithContext(user)                // call .WithContext again per extra scoped context type
    .RunAsync();
```

A scoped tool whose `TScope` has no matching `.WithContext(...)` value throws `AiToolContextException`
to the caller (a wiring mistake) rather than reporting it to the model. Authoring scoped tools is
covered in [`tools.md`](./tools.md#tools-that-need-server-data-the-model-must-not-see-tscope).

> **Cut cost with prompt caching.** An agent resends the system prompt and tool definitions every
> turn, so on Anthropic models set `Cache = Cache.FiveMinutes` (or `Cache.OneHour` for long
> sessions) on the model — the repeated prefix then replays at ~10% of input price from the second
> turn on. Off by default; see [`models.md`](./models.md#prompt-caching-anthropic).

## Builder knobs

Every option is a chainable method on `IAgentRequest<T>` / `IChatRequest<T>`:

| Method | Purpose |
| :--- | :--- |
| `.AddTool<T>()` / `.AddTool(instance)` / `.AddTools(...)` | Expose a tool (DI-resolved or ready-made) |
| `.AddDefaultTools()` / `.AddDefaultMcp()` | **Opt IN** to the globally registered set (off otherwise — registered tooling is never silently active) |
| `.AddMcp(name, url, token, o => o.AllowOnly(...))` | Attach an MCP server with optional tool whitelist |
| `.WithContext(value)` | Trusted server data for scoped tools, never sent to the model |
| `.AllowOnly(names…)` | Restrict the model to these tool names |
| `.OnToolCall((call, ct) => …)` | Return `false` to block a call before it runs |
| `.MaxIterations(n)` | Hard ceiling on agent turns |
| `.MaxParallelToolCalls(n)` | Concurrency within a turn (surplus is queued, never dropped) |
| `.Timeout(t)` | Wall-clock limit for the whole run |
| `.MaxNestedDepth(n)` *(agent only)* | Bound agent-to-tool-to-agent nesting |

## External MCP (client only)

Attach servers on the builder with `.AddMcp(name, url, token, o => o.AllowOnly(...))`; the optional
configure callback whitelists remote tools.

```csharp
await ai.Agent(new GPT5(), prompt)
    .AddMcp("github", "https://mcp.example.com/sse", bearer,   // absolute HTTPS, token optional
            o => o.AllowOnly("read_file", "create_issue"))     // optional whitelist
    .RunAsync();
```

Remote tools are exposed to the model as `"{name}.{tool}"`, for example `github.read_file`.

## Streaming an agent run

`RunStreamAsync()` on the builder emits the agent's events as they happen:

```csharp
await foreach (var evt in ai.Agent(new GPT5(), prompt).AddTool<SaveNoteTool>().RunStreamAsync())
{
    switch (evt)
    {
        case AgentIterationStartedEvent i:   /* i.Iteration */          break;
        case AgentToolCallStartedEvent s:    /* s.ToolName, s.CallId */ break;
        case AgentToolCallCompletedEvent d:  /* d.Invocation */         break;
        case AgentFinalTextEvent f:          /* f.Text */               break;
        case AgentCompletedEvent<Report> c:  /* c.Result */             break;
        case AgentFailedEvent x:             /* x.Error */              break;
    }
}
```

`ai.Chat(llm, system, history).RunStreamAsync()` does the same resuming from a chat transcript
(needs an `IAgentLlm` model). The audit trail (`Iterations`, `ToolCalls`, `Request` and `Total`
usage, `NestedAiCalls`) is documented in [`results.md`](./results.md).

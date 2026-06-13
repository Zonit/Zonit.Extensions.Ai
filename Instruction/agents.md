# The fluent builder: agents and tool-driven chat

The fluent builder is how you run anything with **tools, MCP servers, scoped context, or per-call
limits**. Two entry points on `IAiProvider`, same builder, same terminals:

- `ai.Agent(llm, prompt)` — a single task: prompt in, the model loops over tools, a final answer out.
  Needs an `IAgentLlm` model. Returns `IAgentRequest<T>`.
- `ai.Chat(llm, prompt, history)` — a multi-turn conversation: the system prompt plus a
  `ChatMessage[]` history, optionally tool-driven. Returns `IChatRequest<T>`. See [`chat.md`](./chat.md).

Both are **safe by default**: nothing reaches the model unless you add it. There is no positional
overload that takes `tools` / `mcps` / `context`. For a plain prompt with no tools, use the simple
`GenerateAsync(llm, prompt)` / `ChatAsync(llm, system, history)` calls instead (see
[`usage.md`](./usage.md)). The compact method/return reference also lives in `usage.md`.

## Run an agent

`.RunAsync()` drives the whole loop behind one `await` and returns `ResultAgent<T>` (the answer plus
the full trace and cost — see [`results.md`](./results.md)).

```csharp
ResultAgent<Report> result = await ai.Agent(new GPT5(), new ResearchPrompt { Topic = "EU AI Act" })
    .AddTool<SearchTool>()        // a *Tool.cs you wrote (tools.md), built by DI
    .AddTool<SaveNoteTool>()
    .MaxIterations(12)
    .RunAsync();

Report answer = result.Value;
```

## Stream an agent

`.RunStreamAsync()` is the streaming twin of `.RunAsync()` — same builder, it emits the run as a
sealed `AgentEvent` hierarchy so you can drive a live UI:

```csharp
await foreach (var evt in ai.Agent(new GPT5(), prompt).AddTool<SaveNoteTool>().RunStreamAsync())
{
    switch (evt)
    {
        case AgentIterationStartedEvent i:    /* i.Iteration */          break;
        case AgentTurnCompletedEvent t:       /* model responded */      break;
        case AgentToolCallStartedEvent s:     /* s.ToolName, s.CallId */ break;
        case AgentToolCallCompletedEvent d:   /* d.Invocation */         break;
        case AgentFinalTextEvent f:           /* f.Text */               break;
        case AgentCompletedEvent<Report> c:   /* c.Result */             break;
        case AgentFailedEvent x:              /* x.Error */              break;
    }
}
```

The stream always ends with `AgentFinalTextEvent` + `AgentCompletedEvent<T>` (success) or
`AgentFailedEvent` (failure). `ai.Chat(...).RunStreamAsync()` does the same resuming from history.

## Tools

`.AddTool<T>()` is the recommended path: the DI container builds the tool, injecting its
dependencies, and exposes exactly it. Use `.AddTool(instance)` / `.AddTools(items)` for ready-made
instances in tests or scripts.

```csharp
await ai.Agent(new GPT5(), prompt)
    .AddTool<GetWeatherTool>()
    .AddTool<SaveNoteTool>()
    .RunAsync();
```

### Globally registered tools are opt-in

A tool registered with `AddAiTools<T>()` (see [`tools.md`](./tools.md)) is **off** for every call
unless that call opts in — so one flow's tool never silently leaks into another. Opt in with
`.AddDefaultTools()` (and `.AddDefaultMcp()` for MCP servers); they compose with explicit
`.AddTool<>()` calls.

```csharp
await ai.Agent(new GPT5(), prompt).AddDefaultTools().AddDefaultMcp().RunAsync();
```

## Trusted server data: `.WithContext(...)`

`.WithContext(value)` delivers per-call server data — the current user, tenant, permission scope —
to scoped tools (`ToolBase<TScope, TInput, TOutput>`). Values are matched to each scoped tool's
`TScope` **by type** and are **never** sent to the model, so it cannot read or forge them. Call it
once per distinct context type the exposed tools require.

```csharp
var user = new UserContext(currentUser.Id, currentUser.Name);

await ai.Agent(new GPT5(), prompt)
    .AddTool<GetMyOrdersTool>()      // ToolBase<UserContext, …>
    .WithContext(user)
    .RunAsync();
```

A scoped tool whose `TScope` has no matching `.WithContext(...)` value throws
`AiToolContextException` to the caller (a wiring mistake) rather than reporting it to the model.
Authoring scoped tools: [`tools.md`](./tools.md#tools-that-need-server-data-the-model-must-not-see-tscope).

## External MCP servers

`.AddMcp(name, url, token?, configure?)` attaches a remote Model Context Protocol server over
HTTPS/SSE. The optional `configure` callback whitelists which remote tools are exposed. Remote tools
appear to the model as `"{name}.{tool}"`, e.g. `github.read_file`.

```csharp
await ai.Agent(new GPT5(), prompt)
    .AddMcp("github", "https://mcp.example.com/sse", token,
            o => o.AllowOnly("read_file", "create_issue"))
    .RunAsync();
```

## Limits and gates

| Method | Effect |
| :--- | :--- |
| `.MaxIterations(n)` | Hard ceiling on agent turns |
| `.MaxParallelToolCalls(n)` | Concurrency for tool execution within a turn (surplus is queued, never dropped) |
| `.Timeout(t)` | Wall-clock limit for the whole run |
| `.AllowOnly(names…)` | Restrict the model to these tool names (incl. `"{mcp}.{tool}"`) |
| `.OnToolCall((call, ct) => bool)` | Called before each tool; return `false` to block that call |
| `.MaxNestedDepth(n)` *(agent only)* | Bound agent → tool → agent nesting |

```csharp
await ai.Agent(new GPT5(), prompt)
    .AddTool<SaveNoteTool>()
    .MaxIterations(12)
    .Timeout(TimeSpan.FromMinutes(2))
    .AllowOnly("save_note")
    .OnToolCall(async (call, ct) => call.Name != "delete_everything")
    .RunAsync();
```

Global defaults for these (when a call sets nothing) live under `Ai:Agent` in configuration; see
[`configuration.md`](./configuration.md).

> **Cut cost with prompt caching.** An agent resends the system prompt and tool definitions every
> turn, so on Anthropic models set `Cache = Cache.FiveMinutes` (or `Cache.OneHour`) on the model —
> the repeated prefix then replays at ~10% of input price from the second turn on. Off by default;
> see [`models.md`](./models.md#prompt-caching-anthropic).

## Delegating to a sub-agent

`.AddAgent<T>()` exposes a **sub-agent** — a specialist with its own model, tools and prompt — to the
model as a callable delegation. The parent (often a cheap router/persona model) delegates by the
sub-agent's `Description`; the sub-agent runs its own loop and returns text the parent re-voices.
Trusted `.WithContext(...)` data and (under a chat parent) the conversation are forwarded down. Full
guide: [`subagents.md`](./subagents.md).

```csharp
await ai.Chat(new Haiku45(), routerPrompt, history)
    .AddAgent<ConversionAgent>()      // each: own model + own tools + own prompt
    .AddAgent<SupportAgent>()
    .WithContext(customer)            // forwarded to the sub-agents' scoped tools
    .RunAsync();
```

## The audit trail

`.RunAsync()` returns `ResultAgent<T>` — `Result<T>` plus `Iterations`, `ToolCalls`, `Request` and
`Total` usage roll-ups, and `NestedAiCalls` (cost of AI a tool **or sub-agent** itself called). Full
breakdown in [`results.md`](./results.md).

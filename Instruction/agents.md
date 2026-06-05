# Agents (tool-calling, MCP, audit)

An agent drives the model in a loop: the model requests tool calls, the library runs them in
parallel and feeds the results back, until the model returns a final structured answer, all
behind one `await`. Use the agent overloads of `GenerateAsync` with an `IAgentLlm` model. For
when to choose an agent over a single prompt, see [`usage.md`](./usage.md).

## Run an agent

```csharp
ResultAgent<Report> result = await ai.GenerateAsync(
    new GPT5(),                                   // IAgentLlm
    new ResearchPrompt { Topic = "EU AI Act" },   // typed final answer
    tools: [new SaveNoteTool(store)],             // write *Tool.cs (tools.md)
    mcps:  [new Mcp("github", "https://mcp.example.com/sse", token)],
    options: new AgentOptions { MaxIterations = 12 });

Report answer = result.Value;                     // full trace and cost in result (results.md)
```

Tools and MCP servers passed explicitly are authoritative. Pass `null` to use DI-registered
defaults (`AddAiTools<T>()`, `AddAiMcp(...)`).

> **Cut cost with prompt caching.** An agent resends the system prompt and tool definitions every
> turn, so on Anthropic models set `Cache = Cache.FiveMinutes` (or `Cache.OneHour` for long
> sessions) on the model — the repeated prefix then replays at ~10% of input price from the second
> turn on. Off by default; see [`models.md`](./models.md#prompt-caching-anthropic).

## AgentOptions

| Option | Purpose |
| :--- | :--- |
| `MaxIterations` | Hard ceiling on agent turns |
| `MaxParallelToolCalls` | Concurrency for tool execution within a turn (surplus is queued, never dropped) |
| `Timeout` | Wall-clock limit for the whole run |
| `AllowedTools` | Per-call allow-list of tool names |
| `OnToolCall` | `async (call, ct) => bool`; return `false` to block a call |
| `DefaultTools` / `DefaultMcp` | Opt out of DI-registered defaults for this call |
| `MaxNestedDepth` | Bound agent-to-tool-to-agent nesting |

## External MCP (client only)

```csharp
var mcp = new Mcp(
    name:  "github",
    url:   "https://mcp.example.com/sse",         // absolute HTTPS
    token: bearer,                                // optional
    allowedTools: ["read_file", "create_issue"]); // optional whitelist
```

Remote tools are exposed to the model as `"{name}.{tool}"`, for example `github.read_file`.

## Streaming an agent run

```csharp
await foreach (var evt in ai.GenerateStreamAsync(new GPT5(), prompt, tools: [...]))
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

A second `GenerateStreamAsync` overload takes a prior `chat` transcript to resume a conversation
with full tool-calling. The audit trail (`Iterations`, `ToolCalls`, `Request` and `Total` usage,
`NestedAiCalls`) is documented in [`results.md`](./results.md).

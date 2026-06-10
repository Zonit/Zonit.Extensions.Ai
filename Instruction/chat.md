# Multi-turn chat

A chat is a conversation over a `ChatMessage[]` history. The prompt supplies the **system**
instruction (in chat mode `prompt.Text` is the system message — the inverse of single-shot
`GenerateAsync`, where it is the user message).

Message records:

- `User(string Text, IReadOnlyList<Asset>? Files = null)` — optional per-message attachments.
- `Assistant(string Text)`.
- `Tool` — a tool result; the runtime creates it and returns it in transcripts. You never construct it.

Two ways to run a chat, mirroring the rest of the library: a **simple** call without tools, and the
**fluent** builder when the turn needs tools, MCP or context.

## Simple chat (no tools)

`ChatAsync(llm, prompt, history)` is the plain multi-turn call — no tools, no MCP, no context.

```csharp
var history = new ChatMessage[]
{
    new User("Why does my deployment hang?"),
    new Assistant("Often a missing source-gen override. What error do you see?"),
    new User("CS0534 on .NET 10."),
};

Result<HelpdeskAnswer> result = await ai.ChatAsync(
    new Sonnet46(),
    new HelpdeskPrompt { Product = "Zonit.Ai" },   // system instruction
    history, ct);

Console.WriteLine(result.Value);
```

A plain-`string` overload takes a `string` system prompt instead of an `IPrompt<T>`.

## Tool-driven chat (fluent)

`ai.Chat(llm, prompt, history)` carries the history at the entry point, then exposes the same
safe-by-default builder as an agent (full reference in [`agents.md`](./agents.md)): tools are off
unless you add them. The turn routes through the agent runner, so the result's `.Value` is a
`ResultAgent<T>` when tools ran.

```csharp
var result = await ai.Chat(new GPT5(), prompt, history)
    .AddTool<SaveNoteTool>()
    .MaxIterations(8)
    .RunAsync(ct);
```

For trusted server data the model must not see (current user / tenant), use a scoped tool and
`.WithContext(...)`:

```csharp
var result = await ai.Chat(new GPT5(), prompt, history)
    .AddTool<GetMyOrdersTool>()
    .WithContext(new UserContext(currentUser.Id, currentUser.Name))
    .RunAsync(ct);
```

See [`tools.md`](./tools.md#tools-that-need-server-data-the-model-must-not-see-tscope).

## Streaming

Two modes, by whether the chat uses tools.

**Tool-driven chat → `AgentEvent` stream.** Terminate the builder with `.RunStreamAsync()` (the
streaming twin of `.RunAsync()`) to drive a live UI — tool activity as it happens, then the final
text. Needs an `IAgentLlm` model.

```csharp
await foreach (var evt in ai.Chat(new GPT5(), prompt, history)
                   .AddTool<SaveNoteTool>()
                   .WithContext(user)
                   .RunStreamAsync(ct))
{
    switch (evt)
    {
        case AgentToolCallStartedEvent s:    ui.ShowTool(s.ToolName, s.CallId);  break;
        case AgentToolCallCompletedEvent d:  ui.MarkDone(d.Invocation);          break;
        case AgentFinalTextEvent f:          ui.AppendFinal(f.Text);             break;
        case AgentFailedEvent x:             ui.Error(x.Error);                  break;
    }
}
```

The full `AgentEvent` hierarchy is in [`agents.md`](./agents.md#stream-an-agent).

**Plain chat → token stream.** Without tools, `ChatStreamAsync` emits the reply token by token:

```csharp
await foreach (var token in ai.ChatStreamAsync(new Sonnet46(), prompt, history, ct))
    Console.Write(token);
```

## At a glance

| Call | Tools | Streaming | Returns |
| :--- | :---: | :---: | :--- |
| `ChatAsync(llm, prompt, history)` | no | no | `Result<T>` |
| `ChatStreamAsync(llm, prompt, history)` | no | tokens | `IAsyncEnumerable<string>` |
| `Chat(llm, prompt, history).….RunAsync()` | yes | no | `Result<T>` (`.Value` is a `ResultAgent<T>`) |
| `Chat(llm, prompt, history).….RunStreamAsync()` | yes | events | `IAsyncEnumerable<AgentEvent>` |

Native multi-turn message arrays are used for OpenAI, Anthropic, xAI (Grok) and Google (Gemini);
other providers fall back to a flattened transcript.

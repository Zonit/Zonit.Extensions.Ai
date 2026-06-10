# Multi-turn chat

`ChatAsync` is the conversational counterpart to `GenerateAsync`. The conversation lives in a
`ChatMessage[]` of `User` and `Assistant` records; the prompt supplies the system instruction.
In chat mode `prompt.Text` is the system message, which is the inverse of single-shot
`GenerateAsync`, where it is the user message.

```csharp
var history = new ChatMessage[]
{
    new User("Why does my deployment hang?"),
    new Assistant("Often a missing source-gen override. What error do you see?"),
    new User("CS0534 on .NET 10."),
};

Result<HelpdeskAnswer> result = await ai.ChatAsync(
    llm:    new Sonnet46(),
    prompt: new HelpdeskPrompt { Product = "Zonit.Ai" },   // system instruction
    chat:   history, ct);

Console.WriteLine(result.Value);
```

Message records:

- `User(string Text, IReadOnlyList<Asset>? Files = null)`, with optional per-message attachments.
- `Assistant(string Text)`.
- `Tool` records a tool result. The runtime creates it and returns it in transcripts; you do not
  construct it.

The plain `ChatAsync` overload takes **no** tools, MCP or context — it is the simple multi-turn
call. For a tool-driven conversation, switch to the fluent `ai.Chat(...)` builder below.

## With tools, MCP or context (fluent)

`ai.Chat(llm, prompt, history)` carries the history at the entry point, then takes the same
safe-by-default builder as an agent: tools are off unless you add them. The turn routes through the
agent runner and the result's `.Value` is a `ResultAgent<T>` when tools ran. See
[`agents.md`](./agents.md).

```csharp
var result = await ai.Chat(new GPT5(), prompt, history)
    .AddTool<SaveNoteTool>()
    .MaxIterations(8)
    .RunAsync(ct);
```

To hand tools trusted server data the model must not see (current user/tenant), use a scoped tool
and `.WithContext(...)`:

```csharp
var result = await ai.Chat(new GPT5(), prompt, history)
    .AddTool<GetMyOrdersTool>()
    .WithContext(new UserContext(currentUser.Id, currentUser.Name))
    .RunAsync(ct);
```

See [`tools.md`](./tools.md#tools-that-need-server-data-the-model-must-not-see-tscope). For a live
event stream of a tool-driven chat, terminate with `.RunStreamAsync()` instead (needs an `IAgentLlm`).

## Streaming tokens (no tools)

```csharp
await foreach (var token in ai.ChatStreamAsync(new Sonnet46(), prompt, history, ct))
    Console.Write(token);
```

Every entry point also has a plain-`string` overload: pass a `string` system prompt instead of
an `IPrompt<T>`. Native multi-turn message arrays are used for OpenAI, Anthropic, xAI (Grok) and
Google (Gemini); other providers fall back to a flattened transcript.

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
    llm:    new Sonnet45(),
    prompt: new HelpdeskPrompt { Product = "Zonit.Ai" },   // system instruction
    chat:   history, ct);

Console.WriteLine(result.Value);
```

Message records:

- `User(string Text, IReadOnlyList<Asset>? Files = null)`, with optional per-message attachments.
- `Assistant(string Text)`.
- `Tool` records a tool result. The runtime creates it and returns it in transcripts; you do not
  construct it.

## With tools or MCP

Pass `tools:` and/or `mcps:` to make the turn tool-capable. The call routes through the agent
runner and returns a `ResultAgent<T>`. See [`agents.md`](./agents.md).

```csharp
var result = await ai.ChatAsync(new GPT5(), prompt, history,
    tools: [new SaveNoteTool(store)],
    options: new AgentOptions { MaxIterations = 8 }, cancellationToken: ct);
```

## Streaming tokens (no tools)

```csharp
await foreach (var token in ai.ChatStreamAsync(new Sonnet45(), prompt, history, ct))
    Console.Write(token);
```

Every entry point also has a plain-`string` overload: pass a `string` system prompt instead of
an `IPrompt<T>`. Native multi-turn message arrays are used for OpenAI, Anthropic, xAI (Grok) and
Google (Gemini); other providers fall back to a flattened transcript.

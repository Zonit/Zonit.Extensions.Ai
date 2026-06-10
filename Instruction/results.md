# Results, metadata, token usage and cost

Every call returns `Result<T>`. Read the typed value from `.Value` and everything about the
operation from `.MetaData`. There is no `IsSuccess` property; failures throw.

```csharp
public class Result<T>
{
    public required T        Value    { get; init; }  // the typed answer
    public required MetaData MetaData { get; init; }   // model, usage, cost, timing
}
```

## MetaData

```csharp
var r = await ai.GenerateAsync(new GPT5(), prompt, ct);

r.MetaData.Model        // ILlm: the model instance used
r.MetaData.Provider     // "OpenAI", "Anthropic", ...
r.MetaData.PromptName   // derived from the prompt class, e.g. "Translate"
r.MetaData.Duration     // TimeSpan of the round-trip
r.MetaData.RequestId    // provider request id (nullable)
r.MetaData.Usage        // TokenUsage (below)

// Shortcuts that forward to Usage:
r.MetaData.InputTokens  r.MetaData.OutputTokens  r.MetaData.TotalTokens
r.MetaData.InputCost    r.MetaData.OutputCost    r.MetaData.TotalCost
```

## TokenUsage

`MetaData.Usage` is a `TokenUsage`. Costs are `Price` value objects (from `Zonit.Extensions`),
computed from the model's own pricing.

```csharp
var u = r.MetaData.Usage;
u.InputTokens        // prompt tokens
u.OutputTokens       // completion tokens
u.TotalTokens        // input + output (computed)
u.CachedTokens       // input tokens served from cache, when the provider supports it
u.CacheWriteTokens   // tokens written to cache (Anthropic prompt caching)
u.ReasoningTokens    // thinking tokens (reasoning models); already counted in OutputTokens
u.InputCost          // Price
u.OutputCost         // Price
u.TotalCost          // input + output (computed)
```

```csharp
Console.WriteLine($"{r.MetaData.TotalTokens} tokens, {r.MetaData.TotalCost}");
```

## Estimate or calculate cost

```csharp
// Exact cost from known token counts
Price c = ai.CalculateCost(new GPT5(), inputTokens: 1_000, outputTokens: 500);

// Per modality
Price emb   = ai.CalculateCost(new TextEmbedding3Large(), inputTokens: 1_000);
Price image = ai.CalculateCost(new GPTImage15 { Quality = GPTImage15.QualityType.High,
                                                Size = GPTImage15.SizeType.Square }); // imageCount defaults to 1
Price audio = ai.CalculateCost(new GPT4oTranscribe(), durationSeconds: 180);

// Estimate from prompt text before sending (estimates input tokens for you)
Price est = ai.EstimateCost(new GPT5(), "your prompt text...", estimatedOutputTokens: 500);
```

## Agent runs: `ResultAgent<T> : Result<T>`

A fluent agent or tool-driven chat run — `await ai.Agent(llm, prompt).….RunAsync()` or
`await ai.Chat(llm, system, history).….RunAsync()` (see [`agents.md`](./agents.md)) — returns
`ResultAgent<T>`, which adds the full trace and two usage roll-ups.

```csharp
result.Iterations      // number of model round-trips
result.ToolCalls       // IReadOnlyList<ToolInvocation>: input, output, error, duration, mcp, nested usage
result.Request         // AiUsageSummary of this agent's own model turns
result.Total           // AiUsageSummary of the whole run, including AI called inside tools or sub-agents
result.NestedAiCalls   // flat list of AI calls made inside tools
result.Usage           // the full call tree; drill in through .Children
```

`AiUsageSummary` carries `Tokens` (a `TokenUsage`), `Cost` (equal to `Tokens.TotalCost`),
`Duration` and `Calls`. Use `result.Total.Cost` for end-to-end billing or quota, since it
includes the AI a tool consumed; `result.Request.Cost` attributes cost to the main agent only.

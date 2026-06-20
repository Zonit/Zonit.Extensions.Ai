# Sub-agents: delegate to a specialist (`IAgent` / `AgentBase`)

A **sub-agent** is a named, self-contained agent a parent run can delegate to — the agent-level
counterpart of a tool ([`tools.md`](./tools.md)). The parent (often a cheap router / persona model)
sees only the sub-agent's `Name` and `Description` and decides when to hand work over. The sub-agent
then runs on its **own model**, **own tools** and **own system prompt**, in an isolated loop, and
returns its result to the parent — which re-voices it (translate, apply persona, polish).

Reach for a sub-agent when a capability needs its own big prompt, its own toolset, or a different
(cheaper/stronger) model than the caller. Typical shape: a small router model that delegates to a
`conversion` specialist, a `support` specialist, a `market_analysis` specialist.

- **vs one mega-prompt** — each specialist keeps a focused prompt + tools; adding the next specialist
  doesn't bloat the others.
- **vs a plain tool** — a sub-agent runs a whole tool-using loop on its own model and returns a
  compact answer; a tool is a single typed action.

## Two shapes

| Base class | When the parent calls it | Input |
| :--- | :--- | :--- |
| `AgentBase<TOutput>` | **chat-driven** — reads the forwarded conversation | none |
| `AgentBase<TInput, TOutput>` | **parametrized** — the parent model fills `TInput` | a JSON schema generated from `TInput`; the `Prompt` is a Scriban template `TInput` fills |

## Authoring a chat-driven sub-agent

Override `Name`, `Description`, `Llm`, `Prompt`; add tools with `Toolset.Of<…>()`.

```csharp
using Zonit.Extensions.Ai;

public sealed class ConversionAgent : AgentBase<string>
{
    public override string Name        => "conversion";
    public override string Description => "Onboards a customer onto the exchange: sign-up, KYC, first deposit.";
    public override IAgentLlm Llm      => new Grok43 { MaxTokens = 8_000 };   // its own model
    public override string Prompt      => "You onboard the customer onto the exchange. ...";  // its big system prompt
    public override IReadOnlyList<Type> Tools => Toolset.Of<GenerateLinkTool, ContactSaveTool>(); // its own tools
}
```

## Authoring a parametrized sub-agent (Scriban + input schema)

`TInput` exists to **generate the schema** so the parent model knows what fields to provide. The
agent's `Prompt` is a **Scriban template**; at call time the model's JSON fills it (keys map to
snake_case, exactly like `PromptBase` — see [`prompts.md`](./prompts.md)). There is no separate
deserialization step and nothing to hand-write.

```csharp
using System.ComponentModel;
using Zonit.Extensions.Ai;

public sealed class AnalysisAgent : AgentBase<AnalysisInput, string>
{
    public override string Name        => "market_analysis";
    public override string Description => "Runs market analysis for a symbol over a timeframe.";
    public override IAgentLlm Llm      => new Sonnet46();
    public override string Prompt      => "Analyze {{ symbol }} on the {{ timeframe }} timeframe. Be concise.";
    public override IReadOnlyList<Type> Tools => Toolset.Of<PriceFeedTool>();
}

public sealed class AnalysisInput
{
    [Description("Instrument symbol, e.g. GOLD.")] public required string Symbol    { get; init; }
    [Description("Timeframe, e.g. 1d / 4h.")]      public required string Timeframe { get; init; }
}
```

When the parent calls `market_analysis` with `{"symbol":"GOLD","timeframe":"1d"}`, the sub-agent's
instruction becomes `Analyze GOLD on the 1d timeframe. Be concise.`

## Declaring tools without `typeof`

`Toolset` is the type-safe way to list a sub-agent's tools (each argument is constrained to `ITool`,
so a wrong type is a compile error). Two shapes, pick by count:

```csharp
// Fixed arity — Of<…>() overloads cover one to six tools:
public override IReadOnlyList<Type> Tools => Toolset.Of<GenerateLinkTool, ContactSaveTool>();

// Unbounded — chain Add<T>() as many times as you like (no six-tool ceiling):
public override IReadOnlyList<Type> Tools =>
    Toolset.Add<GenerateLinkTool>().Add<ContactSaveTool>().Add<PriceFeedTool>().Add<RefundTool>();
```

Both return an `IReadOnlyList<Type>` and are `typeof`-free and AOT-clean. (For a *dynamic* set you can
still return any `IReadOnlyList<Type>` you build yourself.) Each tool type must be DI-resolvable —
register it with `AddAiTools<T>()` ([`tools.md`](./tools.md)).

## Giving the sub-agent its own MCP servers

A sub-agent can connect to external MCP servers, alongside its own `Tools`, by overriding `Mcps`.
Each `Mcp` is connected when the sub-agent runs and its remote tools are exposed to the sub-agent's
model under the `"{Name}.{tool}"` prefix (filtered by the optional whitelist). Declare them with a
collection expression:

```csharp
public sealed class ResearchAgent : AgentBase<string>
{
    public override string Name        => "research";
    public override string Description => "Researches a topic using web + GitHub MCP servers.";
    public override IAgentLlm Llm      => new Sonnet46();
    public override string Prompt      => "Research the user's topic and summarise the findings.";

    public override IReadOnlyList<Type> Tools => Toolset.Of<SummariseTool>();   // its own local tools
    public override IReadOnlyList<Mcp>  Mcps  =>                                // ...plus MCP servers
    [
        new("github", "https://mcp.example.com/sse", githubToken, new[] { "read_file", "search_code" }),
        new("web",    "https://web-mcp.example.com/sse"),
    ];
}
```

The fourth argument is an optional tool whitelist (without the `"{Name}."` prefix): `null` exposes
every tool the server reports, an empty list exposes none. The parent's MCP servers are **not**
inherited — a sub-agent only sees the servers it declares here, the same way it only sees its own
`Tools`. MCP servers are an HTTPS client connection ([`agents.md`](./agents.md)).

## Forwarding the conversation: `ForwardChat`

When the **parent is a chat** (`ai.Chat(...).AddAgent<T>()`), the conversation is forwarded to the
sub-agent as its history — **for both shapes**, so a parametrized agent still sees the conversation
alongside its rendered task. This is on by default. Override `ForwardChat => false` to run the
sub-agent isolated from the conversation. A parent started as a plain agent run (`ai.Agent(...)`)
has no conversation, so nothing is forwarded regardless.

```csharp
public override bool ForwardChat => false;   // run isolated, even under a chat parent
```

## Trusted context flows down to the sub-agent's tools

`.WithContext(...)` on the parent is forwarded into the sub-agent, so its tools receive the same
trusted `IRunContext` values (read with `context.Get<T>()`) that the model never sees — the sub-agent
itself doesn't read it, it just carries it through. See
[`tools.md`](./tools.md#reading-trusted-server-data-the-model-must-not-see-iruncontext).

## Showing a sub-agent only when the context allows: `IsAvailable`

Override `IsAvailable(IRunContext context)` to gate whether the parent model is even **shown** this
sub-agent. It returns `true` by default; return `false` and the sub-agent is omitted from the parent's
tool set, so the model cannot delegate to it. Drive it from trusted context — permissions, plan,
tenant — to express access rules declaratively:

```csharp
public override bool IsAvailable(IRunContext context) => context.Has<AdminPass>();
```

It is evaluated **once** when the run's tool set is assembled (a sub-agent can't be removed mid-run);
the next run re-evaluates against a possibly refreshed context. Keep it synchronous and side-effect
free — load any permission data into the context *before* the run rather than doing I/O here.

### Gating on the conversation: `ConversationInfo`

The framework seeds one value into the run context itself: `ConversationInfo`, carrying
`MessageCount` (the messages forwarded into this run) and a derived `IsEmpty`. Read it like any other
context value to gate a sub-agent on whether the conversation has started — e.g. an "opener" that
greets the user, shown only on an empty conversation:

```csharp
public override bool IsAvailable(IRunContext context)
    => context.Get<ConversationInfo>()?.IsEmpty == true;
```

The count reflects the conversation as it stood when the run began, so it is valid in `IsAvailable`
(evaluated before the loop) and in a tool's `ExecuteAsync`. A plain `ai.Agent(...)` run has no
history, so `MessageCount` is `0`. Each sub-agent run is seeded with its own `ConversationInfo`.

## Registering and exposing

Register the sub-agent and its tools, then expose it on a parent run with `.AddAgent<T>()` (works on
both `ai.Agent(...)` and `ai.Chat(...)`):

```csharp
builder.Services.AddAiAgent<ConversionAgent>();
builder.Services.AddAiAgent<AnalysisAgent>();
builder.Services.AddAiTools<GenerateLinkTool>();   // the sub-agents' tools
builder.Services.AddAiTools<ContactSaveTool>();
builder.Services.AddAiTools<PriceFeedTool>();
```

## A router that delegates (the common shape)

A cheap model reads the conversation, delegates to the right specialist, and re-voices the reply in
the customer's language. Trusted context (the customer) is forwarded to every specialist's tools.

```csharp
var reply = await ai.Chat(new Haiku45(), routerSystemPrompt, history)
    .AddAgent<ConversionAgent>()       // each specialist: own model, own prompt, own tools
    .AddAgent<SupportAgent>()
    .AddAgent<AnalysisAgent>()
    .WithContext(customerContext)      // forwarded down to each sub-agent's scoped tools
    .RunAsync();
```

The router model picks a specialist by its `Description`, the specialist does the real work, and the
router writes the final message — so the persona and language stay in one place.

## What the parent gets back, cost, and limits

The sub-agent's **final text** is returned to the parent as the delegation tool's result, ready for
the parent to re-voice. The sub-agent's tokens and cost roll into the parent's `ResultAgent.Total`
and appear in `NestedAiCalls` ([`results.md`](./results.md)); agent → tool → agent nesting is bounded
by `.MaxNestedDepth(n)` and surfaces `AiNestingLimitException` if exceeded. Both shapes are
AOT-clean (no reflection on the invocation path).

## Rules

- Inherit `AgentBase<TOutput>` (chat-driven) or `AgentBase<TInput, TOutput>` (parametrized); name the
  file `*Agent.cs`.
- `Name` is the delegation function name the parent model sees; `Description` says what the sub-agent
  does and when to delegate — the model relies on it, so write it well.
- `Llm` is any `IAgentLlm` — route specialised work to a fitting (cheaper / stronger) model.
- `Tools` lists the sub-agent's own tools with `Toolset.Of<…>()` (≤6) or `Toolset.Add<…>().Add<…>()`
  (unbounded); register each with `AddAiTools<T>()`.
- `Mcps` lists the sub-agent's own MCP servers (optional, empty by default) — declared with a
  collection expression of `new Mcp(...)`; not inherited from the parent.
- Parametrized: `Prompt` is a Scriban template, `TInput` defines the fields — never hand-write the schema.
- `ForwardChat` is `true` by default (forward the conversation under a chat parent); set `false` to isolate.
- The sub-agent returns its final text; the parent re-voices it. Register with `AddAiAgent<T>()`,
  expose with `.AddAgent<T>()`.

The agent loop, tools, MCP and the `ResultAgent<T>` audit trail are in [`agents.md`](./agents.md).

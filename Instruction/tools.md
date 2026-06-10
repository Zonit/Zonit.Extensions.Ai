# Writing an agent tool (`*Tool.cs`)

A tool is a typed action the model can call during an agent run. Inherit
`ToolBase<TInput, TOutput>`. The library generates the input JSON Schema, deserializes the
model's arguments, serializes your result, and traps exceptions.

## Template

```csharp
using System.ComponentModel;
using Zonit.Extensions.Ai;

public sealed class SaveNoteTool(INoteStore store)
    : ToolBase<SaveNoteTool.Input, SaveNoteTool.Output>
{
    public override string Name        => "save_note";
    public override string Description => "Persists a note and returns its id.";

    public override async Task<Output> ExecuteAsync(Input input, CancellationToken ct)
    {
        var id = await store.SaveAsync(input.Title, input.Body, ct);
        return new Output { Id = id };
    }

    public sealed class Input
    {
        [Description("Short title.")]      public required string Title { get; init; }
        [Description("Note body / text.")] public required string Body  { get; init; }
    }

    public sealed class Output { public Guid Id { get; init; } }
}
```

## Rules

- Inherit `ToolBase<TInput, TOutput>` (`TInput` must be a `class`) and name the file `*Tool.cs`.
- `Name` is the snake_case function name the model sees. `Description` states what the tool does
  and when to use it; the model relies on this, so write it well.
- `TInput` and `TOutput` are plain DTOs. Annotate `TInput` members with `[Description]`. The
  schema is generated and cached automatically, so do not hand-write JSON schema.
- Override `ExecuteAsync` and take DI dependencies through the primary constructor.
- Throwing is fine. The runner returns the error to the model (default
  `ToolExceptionPolicy.ReturnErrorToModel`), which can retry or fall back.

## Using a tool

```csharp
// Fluent (recommended): the container builds the tool, dependencies injected.
await ai.Agent(new GPT5(), prompt).AddTool<SaveNoteTool>().RunAsync();

// Per call, explicit instance (for tests / ready-made tools):
await ai.Agent(new GPT5(), prompt).AddTool(new SaveNoteTool(store)).RunAsync();

// As a global default — OFF unless a call opts in:
builder.Services.AddAiTools<SaveNoteTool>();      // by type
builder.Services.AddAiTools(new ReportBugTool()); // by instance
```

Globally registered tools are **opt-in**, never silently active. A call that passes `tools: null`
(or omits it) exposes none of them; opt in per call with `.AddDefaultTools()` (fluent) or
`options: new AgentOptions { DefaultTools = true }`. This keeps a tool registered for one flow from
leaking into every other agent call. Either way, the model's `TInput` is always just a filter — the
authorization key comes from `TScope` (below).

## Tools that need server data the model must not see (`TScope`)

When a tool must act on trusted data — the current user's id, the tenant, a permission scope —
**do not** put it in `TInput`. Anything in `TInput` is in the schema, so the model supplies it and
can forge it. Instead inherit `ToolBase<TScope, TInput, TOutput>`: `TScope` comes **first** (server
data), then the model's `TInput`. The caller passes the value per call via `context:`; it is never
sent to the model.

```csharp
public sealed record UserContext(Guid UserId, string UserName, Guid TenantId);

public sealed class GetMyOrdersTool(IOrderRepository orders)
    : ToolBase<UserContext, GetMyOrdersTool.Input, GetMyOrdersTool.Output>
{
    public override string Name        => "get_my_orders";
    public override string Description => "Lists the signed-in user's orders.";

    // context = trusted server data (first); input = model arguments (second).
    public override async Task<Output> ExecuteAsync(
        UserContext context, Input input, CancellationToken ct)
    {
        var rows = await orders.GetForUserAsync(context.UserId, input.Status, ct);
        return new Output { Count = rows.Count };
    }

    public sealed class Input
    {
        [Description("Optional status filter, e.g. 'pending'.")] public string? Status { get; init; }
    }
    public sealed class Output { public int Count { get; init; } }
}
```

Supply the context on the builder with `.WithContext(...)` (matched to each tool's `TScope` by type):

```csharp
var user = new UserContext(currentUser.Id, currentUser.Name, currentUser.TenantId);

await ai.Agent(new GPT5(), prompt)
    .AddTool<GetMyOrdersTool>()
    .WithContext(user)                      // one context
    .RunAsync();

await ai.Agent(new GPT5(), prompt)          // several scoped tools, several contexts
    .AddTool<GetMyOrdersTool>()
    .AddTool<BillingTool>()
    .WithContext(user)
    .WithContext(billing)
    .RunAsync();
```

Rules:

- **`context` first, `input` second** in `ExecuteAsync`. `TScope` must be a `class`/`record`.
- **`context` is guaranteed non-null and correctly typed** — the runner resolves it from the
  `context:` list before calling. You never write `if (context is null)` for a *missing* context.
- **Missing or mistyped context is a wiring error, not a model error.** If a scoped tool runs but no
  matching value was passed, the runner throws `AiToolContextException` to *you* (the caller), so it
  surfaces in logs/tests at first run instead of leaking to the model. Validate the context's
  *contents* (permissions, etc.) yourself — throwing there is reported to the model like any tool error.
- **Add it exactly like a plain tool** (`.AddTool<GetMyOrdersTool>()`). Only `.WithContext(...)` is
  new, and only scoped tools read it; plain tools ignore it.

The agent loop, MCP and the `ResultAgent<T>` audit trail are in [`agents.md`](./agents.md).

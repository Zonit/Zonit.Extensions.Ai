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

    // context = trusted server data (first, never seen by the model); input = model arguments.
    public override async Task<Output> ExecuteAsync(IRunContext context, Input input, CancellationToken ct)
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
- Override `ExecuteAsync`. Its first parameter is the run's `IRunContext` — trusted server data the
  model never sees (see below); ignore it for tools that need none. Take DI dependencies through the
  primary constructor.
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
authorization key comes from `IRunContext` (below).

## Reading trusted server data the model must not see (`IRunContext`)

When a tool must act on trusted data — the current user's id, the tenant, a permission scope —
**do not** put it in `TInput`. Anything in `TInput` is in the schema, so the model supplies it and
can forge it. Instead read it from the `IRunContext context` parameter (first argument of
`ExecuteAsync`). The caller supplies the values per call with `.WithContext(...)`; they are kept on
the server and never sent to the model.

The context is a **typed bag**: register as many models as you like and pull the ones a given tool
needs with `context.Get<T>()` (returns `null` when absent) or `context.GetRequired<T>()` (throws when
absent). No more single overloaded context object.

```csharp
public sealed record UserContext(Guid UserId, string UserName, Guid TenantId);

public sealed class GetMyOrdersTool(IOrderRepository orders)
    : ToolBase<GetMyOrdersTool.Input, GetMyOrdersTool.Output>
{
    public override string Name        => "get_my_orders";
    public override string Description => "Lists the signed-in user's orders.";

    public override async Task<Output> ExecuteAsync(IRunContext context, Input input, CancellationToken ct)
    {
        var user = context.GetRequired<UserContext>();   // trusted; the model never saw it
        var rows = await orders.GetForUserAsync(user.UserId, input.Status, ct);
        return new Output { Count = rows.Count };
    }

    public sealed class Input
    {
        [Description("Optional status filter, e.g. 'pending'.")] public string? Status { get; init; }
    }
    public sealed class Output { public int Count { get; init; } }
}
```

Supply the values on the builder with `.WithContext(...)` (call it once per distinct type):

```csharp
var user = new UserContext(currentUser.Id, currentUser.Name, currentUser.TenantId);

await ai.Agent(new GPT5(), prompt)
    .AddTool<GetMyOrdersTool>()
    .WithContext(user)                      // one model
    .RunAsync();

await ai.Agent(new GPT5(), prompt)          // many models — each tool picks what it needs
    .AddTool<GetMyOrdersTool>()
    .AddTool<BillingTool>()
    .WithContext(user)
    .WithContext(billing)
    .RunAsync();
```

### Writing back: keep server-resolved values out of the model

The bag holds your instances **by reference**, so a tool can write a value into the context instead
of returning it through the model — useful when the value must not round-trip through the token
stream (where the model could alter it). A `get; set;` property is writable; `get; init;` / get-only
is read-only; replace a whole immutable value (a record) with `context.Set<T>(newValue)`.

```csharp
// "Which worker should I assign?" — resolve it server-side and stamp the context.
var worker = context.GetRequired<WorkerModel>();
worker.Id   = resolved.Id;     // later tools and the host read this; the model never sees the id
worker.Name = resolved.Name;
```

Rules:

- **`context` first, `input` second** in `ExecuteAsync`. The bag is never null (empty when the caller
  supplied no context); read types with `Get<T>()` / `GetRequired<T>()` (`T` is a `class`/`record`).
- **A missing *required* value is a wiring error, not a model error.** `GetRequired<T>()` with no
  matching value (or an ambiguous match) throws `AiToolContextException` to *you* (the caller), so it
  surfaces in logs/tests at first run instead of leaking to the model. Use `Get<T>()` when absence is
  acceptable. Validate a value's *contents* (permissions, etc.) yourself — throwing there is reported
  to the model like any tool error.
- **Add a tool exactly the same way** (`.AddTool<GetMyOrdersTool>()`); only `.WithContext(...)` is
  new. Tools that read no context simply ignore the parameter.
- **Mutations are shared.** Tools run in parallel, so two tools writing the *same* model concurrently
  race — design the model accordingly (or use `init`-only for read-only context).

The agent loop, MCP and the `ResultAgent<T>` audit trail are in [`agents.md`](./agents.md). To bundle a
whole specialist — its own model, prompt and tools — that the model can delegate to, write a
**sub-agent** instead of a tool: [`subagents.md`](./subagents.md).

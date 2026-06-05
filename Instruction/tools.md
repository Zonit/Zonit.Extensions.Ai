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
// Per call (authoritative; DI defaults are ignored when you pass a list):
await ai.GenerateAsync(new GPT5(), prompt, tools: [new SaveNoteTool(store)]);

// As a DI default (used when a call passes tools: null):
builder.Services.AddAiTools<SaveNoteTool>();      // by type
builder.Services.AddAiTools(new ReportBugTool()); // by instance
```

The agent loop, MCP and the `ResultAgent<T>` audit trail are in [`agents.md`](./agents.md).

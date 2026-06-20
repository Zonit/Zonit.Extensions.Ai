using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Base class for custom agent tools. Analogous to <c>PromptBase&lt;T&gt;</c>:
/// override <see cref="Name"/>, <see cref="Description"/> and
/// <see cref="ExecuteAsync"/> — the library handles JSON schema generation,
/// deserialization, serialization and exception trapping for you.
/// </summary>
/// <typeparam name="TInput">Input DTO. Schema generated automatically from its public properties and <c>[Description]</c> attributes.</typeparam>
/// <typeparam name="TOutput">Output DTO. Serialized back to JSON for the model.</typeparam>
/// <remarks>
/// <para>
/// The schema is computed once per type and cached. Deserialization uses
/// <see cref="JsonSerializer"/> with camelCase naming, matching the output of
/// <see cref="JsonSchemaGenerator"/>.
/// </para>
/// <para>
/// <see cref="ExecuteAsync"/> receives the run's <see cref="IRunContext"/> bag <b>first</b>: trusted
/// server data (current user / tenant / permissions) supplied via the agent call's
/// <c>WithContext(...)</c> and never exposed to the model. Read only the models you need with
/// <c>context.Get&lt;T&gt;()</c> / <c>context.GetRequired&lt;T&gt;()</c>; the bag is empty (not null)
/// when the caller supplied no context. You can also mutate a context model in place — e.g. write the
/// id of a record you resolved — so later tools and the host see it without it round-tripping through
/// the model.
/// </para>
/// <para>
/// You may throw any exception from <see cref="ExecuteAsync"/>. The agent
/// runner catches it and forwards the error to the model as a tool result
/// (see <see cref="ToolExceptionPolicy"/>). Claude and GPT models handle
/// such errors gracefully — they can retry with different arguments,
/// fall back to another tool, or explain the failure to the user.
/// (An <see cref="AiToolContextException"/> — e.g. from <c>GetRequired&lt;T&gt;()</c> — is the
/// exception: it signals a wiring mistake and propagates to the caller, never to the model.)
/// </para>
/// </remarks>
[RequiresUnreferencedCode("ToolBase uses reflection to build a JSON schema from TInput.")]
[RequiresDynamicCode("ToolBase uses reflection and runtime JSON (de)serialization.")]
public abstract class ToolBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TInput,
    TOutput> : IContextualTool
    where TInput : class
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    // Schema is built once per concrete tool type. Prefers the AOT-safe schema emitted at
    // build time by AiJsonSchemaGenerator for every ToolBase<TInput,_>; falls back to the
    // reflection-based JsonSchemaGenerator (inside AiSchemaRegistry) only when the generator
    // did not see this tool. Note: this base remains [RequiresUnreferencedCode] regardless —
    // tool arguments are still (de)serialized reflectively below.
    private static readonly Lazy<JsonElement> _schema = new(
        () => AiSchemaRegistry.GetSchema(typeof(TInput)),
        isThreadSafe: true);

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public JsonElement InputSchema => _schema.Value;

    /// <summary>
    /// Executes the tool logic. <paramref name="context"/> is the run's trusted context bag (never
    /// null; empty when no context was supplied) — read your server data with
    /// <c>context.Get&lt;T&gt;()</c>, never from <paramref name="input"/>, which the model controls.
    /// Throw any exception to signal an error — the runner wraps it into a tool result the model can
    /// react to (default policy <see cref="ToolExceptionPolicy.ReturnErrorToModel"/>); an
    /// <see cref="AiToolContextException"/> instead propagates to the caller.
    /// </summary>
    /// <param name="context">The run's <see cref="IRunContext"/> bag. Never seen by the model.</param>
    /// <param name="input">Deserialized arguments supplied by the model.</param>
    /// <param name="cancellationToken">Cancellation token, honored by the agent runner's timeouts.</param>
    public abstract Task<TOutput> ExecuteAsync(IRunContext context, TInput input, CancellationToken cancellationToken);

    /// <inheritdoc />
    async Task<JsonElement> IContextualTool.InvokeAsync(JsonElement arguments, IRunContext context, CancellationToken cancellationToken)
    {
        var input = Deserialize(arguments);
        var output = await ExecuteAsync(context, input, cancellationToken).ConfigureAwait(false);
        return Serialize(output);
    }

    // Context-less path: only hit when a tool is invoked outside the agent loop (e.g. directly in a
    // test, or an external runtime that didn't bind context). Runs with an empty context — Get<T>()
    // returns null and GetRequired<T>() throws, exactly as for a run with no WithContext(...) values.
    async Task<JsonElement> ITool.InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var input = Deserialize(arguments);
        var output = await ExecuteAsync(new RunContext(), input, cancellationToken).ConfigureAwait(false);
        return Serialize(output);
    }

    private static TInput Deserialize(JsonElement arguments)
    {
        // Model can legitimately return an empty object for tools with no required parameters.
        if (arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return JsonSerializer.Deserialize<TInput>("{}", _serializerOptions)
                ?? throw new InvalidOperationException(
                    $"Failed to create an empty {typeof(TInput).Name} instance.");
        }

        var raw = arguments.GetRawText();
        return JsonSerializer.Deserialize<TInput>(raw, _serializerOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize tool arguments into {typeof(TInput).Name}. " +
                "Ensure the model's output matches the schema.");
    }

    private static JsonElement Serialize(TOutput value)
    {
        var json = JsonSerializer.Serialize(value, _serializerOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Base class for tools that need per-call <b>server context</b> (<typeparamref name="TScope"/>)
/// alongside the model-provided <typeparamref name="TInput"/>. Mirrors
/// <see cref="ToolBase{TInput, TOutput}"/> — the library generates the input JSON schema,
/// deserializes the model's arguments, serializes your result and traps exceptions — but adds
/// a first parameter carrying trusted data the model never sees.
/// </summary>
/// <typeparam name="TScope">
/// Server context (e.g. the current user/tenant). Supplied per call via the agent API's
/// <c>context:</c> argument and matched to this type. <b>Not</b> part of the schema, so the
/// model cannot read or forge it — ideal for ids and identities that must come from the server.
/// </typeparam>
/// <typeparam name="TInput">Input DTO from the model. Schema generated from its public properties and <c>[Description]</c> attributes.</typeparam>
/// <typeparam name="TOutput">Output DTO. Serialized back to JSON for the model.</typeparam>
/// <remarks>
/// <para>
/// In <see cref="ExecuteAsync"/>, <c>context</c> comes <b>first</b> (server data), then
/// <c>input</c> (model data). It is guaranteed non-null and of the correct type: the agent
/// runner resolves it from the call's <c>context:</c> list before invoking and throws
/// <see cref="AiToolContextException"/> to the caller if no matching value was supplied — so
/// you never write null-checks for a missing context. Validate the context's <i>contents</i>
/// (e.g. permissions) yourself; throwing there is reported to the model like any tool error.
/// </para>
/// <para>
/// Register and pass exactly like a plain tool (<c>AddAiTools&lt;T&gt;()</c> or <c>tools: [...]</c>).
/// Supply the context on the call: <c>context: [user]</c>, or <c>context: [user, billing]</c>
/// when several scoped tools each need their own type. See <c>tools.md</c> / <c>agents.md</c>.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("ToolBase uses reflection to build a JSON schema from TInput.")]
[RequiresDynamicCode("ToolBase uses reflection and runtime JSON (de)serialization.")]
public abstract class ToolBase<
    TScope,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TInput,
    TOutput> : IScopedTool
    where TScope : class
    where TInput : class
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    // Schema is built once per concrete tool type — same path as ToolBase<TInput, TOutput>.
    private static readonly Lazy<JsonElement> _schema = new(
        () => AiSchemaRegistry.GetSchema(typeof(TInput)),
        isThreadSafe: true);

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public JsonElement InputSchema => _schema.Value;

    Type IScopedTool.ContextType => typeof(TScope);

    /// <summary>
    /// Executes the tool logic. <paramref name="context"/> is the trusted server context
    /// (guaranteed non-null and of type <typeparamref name="TScope"/>); <paramref name="input"/>
    /// is the model-supplied, schema-validated argument. Throw any exception to signal an error —
    /// the runner wraps it into a tool result the model can react to
    /// (default <see cref="ToolExceptionPolicy.ReturnErrorToModel"/>).
    /// </summary>
    /// <param name="context">Per-call server context. Never seen by the model.</param>
    /// <param name="input">Deserialized arguments supplied by the model.</param>
    /// <param name="cancellationToken">Cancellation token, honored by the agent runner's timeouts.</param>
    public abstract Task<TOutput> ExecuteAsync(TScope context, TInput input, CancellationToken cancellationToken);

    async Task<JsonElement> IScopedTool.InvokeAsync(JsonElement arguments, object context, CancellationToken cancellationToken)
    {
        // The runner guarantees a non-null context assignable to TScope before calling.
        var input = Deserialize(arguments);
        var output = await ExecuteAsync((TScope)context, input, cancellationToken).ConfigureAwait(false);
        return Serialize(output);
    }

    // A scoped tool is always dispatched through IScopedTool.InvokeAsync (with context).
    // This path means the runner mis-routed it — fail loudly rather than run without context.
    Task<JsonElement> ITool.InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            $"Tool '{Name}' is a scoped tool (ToolBase<TScope, TInput, TOutput>) and must be " +
            "invoked with a context. Supply it via the agent call's 'context:' argument.");

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

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
/// You may throw any exception from <see cref="ExecuteAsync"/>. The agent
/// runner catches it and forwards the error to the model as a tool result
/// (see <see cref="ToolExceptionPolicy"/>). Claude and GPT models handle
/// such errors gracefully — they can retry with different arguments,
/// fall back to another tool, or explain the failure to the user.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("ToolBase uses reflection to build a JSON schema from TInput.")]
[RequiresDynamicCode("ToolBase uses reflection and runtime JSON (de)serialization.")]
public abstract class ToolBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TInput,
    TOutput> : ITool
    where TInput : class
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    // Schema is expensive to build — cache it per concrete tool type.
    private static readonly Lazy<JsonElement> _schema = new(
        () => JsonSchemaGenerator.Generate(typeof(TInput)),
        isThreadSafe: true);

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public JsonElement InputSchema => _schema.Value;

    /// <summary>
    /// Executes the tool logic. Throw any exception to signal an error —
    /// the runner will wrap it into a tool-result the model can react to
    /// (default policy <see cref="ToolExceptionPolicy.ReturnErrorToModel"/>).
    /// </summary>
    /// <param name="input">Deserialized arguments supplied by the model.</param>
    /// <param name="cancellationToken">Cancellation token, honored by the agent runner's timeouts.</param>
    public abstract Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken);

    /// <inheritdoc />
    async Task<JsonElement> ITool.InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var input = Deserialize(arguments);
        var output = await ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
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

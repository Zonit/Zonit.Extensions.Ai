using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// A custom agent tool that the library executes locally (or via an MCP adapter)
/// during the agent loop.
/// </summary>
/// <remarks>
/// This is the low-level, non-generic contract. In application code prefer the
/// typed base class <c>ToolBase&lt;TInput, TOutput&gt;</c> (in <c>Zonit.Extensions.Ai</c>)
/// — it implements <see cref="InvokeAsync"/> and <see cref="InputSchema"/> for you.
/// <para>
/// The agent runner calls <see cref="InvokeAsync"/> with the arguments returned by
/// the model. If the tool throws, the runner catches the exception and forwards
/// it to the model as a tool result (see <see cref="ToolExceptionPolicy"/>).
/// </para>
/// <para>
/// Note: this interface is distinct from <see cref="IToolBase"/>, which represents
/// provider-native tools (web search, file search, code interpreter). Provider-native
/// tools are executed on the provider side and belong to <see cref="ILlm.Tools"/>.
/// </para>
/// </remarks>
public interface ITool
{
    /// <summary>
    /// Unique name visible to the model (used as the function name).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description shown to the model — explain what the tool does and when to use it.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema describing the tool's input parameters.
    /// For <c>ToolBase&lt;TInput, TOutput&gt;</c> this is generated automatically from <c>TInput</c>.
    /// </summary>
    JsonElement InputSchema { get; }

    /// <summary>
    /// Executes the tool with the raw JSON arguments received from the model
    /// and returns the raw JSON result.
    /// </summary>
    /// <param name="arguments">Arguments produced by the model, validated against <see cref="InputSchema"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool output as a <see cref="JsonElement"/>.</returns>
    Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken);
}

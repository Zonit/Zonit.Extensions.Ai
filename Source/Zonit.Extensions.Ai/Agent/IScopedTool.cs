using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// A tool that, in addition to the model-provided arguments, receives a per-call,
/// server-provided <i>context</i> object that is never exposed to the model.
/// Implemented by <see cref="ToolBase{TScope, TInput, TOutput}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the internal dispatch contract used by the agent runner. The context is
/// supplied by the caller via the agent call's <c>context:</c> argument (an
/// <see cref="IReadOnlyList{T}"/> of <see cref="object"/>) and resolved to the tool's
/// <see cref="ContextType"/> by exact-or-assignable type match. Because the model never
/// sees it, it cannot be forged through the prompt — the value flows straight from the
/// server, through the pipeline, into <see cref="InvokeAsync"/>.
/// </para>
/// <para>
/// The runner validates the context <b>before</b> calling: if no value of
/// <see cref="ContextType"/> was supplied it throws <see cref="AiToolContextException"/>
/// to the caller (a wiring mistake), rather than reporting the error to the model.
/// </para>
/// </remarks>
internal interface IScopedTool : ITool
{
    /// <summary>CLR type of the required context (the tool's <c>TScope</c>).</summary>
    Type ContextType { get; }

    /// <summary>
    /// Executes the tool with the raw JSON arguments from the model and the resolved,
    /// non-null context object (guaranteed assignable to <see cref="ContextType"/>).
    /// </summary>
    Task<JsonElement> InvokeAsync(JsonElement arguments, object context, CancellationToken cancellationToken);
}

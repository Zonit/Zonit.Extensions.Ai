using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Internal dispatch contract for a tool that receives the run's <see cref="IRunContext"/> bag in
/// addition to the model-provided arguments. Implemented by <see cref="ToolBase{TInput, TOutput}"/>.
/// </summary>
/// <remarks>
/// The agent runner routes here (with the run's shared context) instead of the plain
/// <see cref="ITool.InvokeAsync(JsonElement, CancellationToken)"/> path, so a tool can read trusted
/// server data — and mutate it for later tools — without the model ever seeing it. Replaces the
/// former single-type <c>IScopedTool</c>: the bag resolves any number of context types on demand,
/// so a tool is no longer limited to one <c>TScope</c>.
/// </remarks>
internal interface IContextualTool : ITool
{
    /// <summary>
    /// Executes the tool with the raw JSON arguments from the model and the run's context bag
    /// (never null; empty when the caller supplied no context).
    /// </summary>
    Task<JsonElement> InvokeAsync(JsonElement arguments, IRunContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Internal marker for a tool whose exposure to the model depends on the run's <see cref="IRunContext"/>.
/// Evaluated once when the tool set is assembled; a <c>false</c> result removes the tool (or sub-agent)
/// from the set the model sees. Implemented by <see cref="AgentToolAdapter"/>, delegating to
/// <see cref="IAgent.IsAvailable"/>.
/// </summary>
internal interface IConditionalTool : ITool
{
    /// <summary>Whether this tool should be exposed to the model given the trusted context.</summary>
    bool IsAvailable(IRunContext context);
}

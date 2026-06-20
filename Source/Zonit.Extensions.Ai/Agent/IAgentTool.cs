using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Internal dispatch contract for a tool whose body runs a nested sub-agent (an <see cref="IAgent"/>
/// exposed to a parent). Unlike a plain <see cref="ITool"/>, the agent runner hands it the parent's
/// <see cref="IRunContext"/> bag and the seeded conversation, so the sub-agent can forward both down to
/// its own model and tools.
/// </summary>
/// <remarks>
/// Implemented by <see cref="AgentToolAdapter"/>. The runner detects this contract in
/// <c>ToolExecutor</c> and routes the call here (with context + chat) instead of the plain
/// <see cref="ITool.InvokeAsync(JsonElement, CancellationToken)"/> path. It is distinct from
/// <see cref="IContextualTool"/>, which is a leaf tool: an agent tool additionally forwards the
/// conversation so its sub-agent runs in context.
/// </remarks>
internal interface IAgentTool : ITool
{
    /// <summary>
    /// Runs the sub-agent. <paramref name="context"/> is the parent's trusted context bag (never seen
    /// by any model); <paramref name="chat"/> is the conversation seeded into the parent run, forwarded
    /// as the sub-agent's history for chat-driven agents.
    /// </summary>
    Task<JsonElement> InvokeAsync(
        JsonElement arguments,
        IRunContext context,
        IReadOnlyList<ChatMessage>? chat,
        CancellationToken cancellationToken);
}

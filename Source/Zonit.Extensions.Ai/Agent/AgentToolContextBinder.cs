using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Adapts the framework's tools for an <b>external</b> agent runtime that executes tools itself —
/// primarily the Claude Code CLI (<c>claude -p</c>) reached over the loopback MCP bridge — which can
/// only invoke the context-less <see cref="ITool.InvokeAsync(JsonElement, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// In-process, <see cref="ToolExecutor"/> dispatches contextual tools (<see cref="IContextualTool"/>) with
/// the run's <see cref="IRunContext"/> bag and sub-agent tools (<see cref="IAgentTool"/>) with the bag and
/// seeded chat. An external runtime cannot — so each such tool is wrapped here in a plain
/// <see cref="ITool"/> that closes over the captured context (and chat) and injects it on call, exactly as
/// the in-process executor would. Plain tools are returned unchanged. Name/description/schema are forwarded
/// verbatim, so the model sees an identical tool set; only the invocation path differs.
/// </remarks>
internal static class AgentToolContextBinder
{
    /// <summary>
    /// Returns <paramref name="tools"/> with every contextual/sub-agent tool wrapped so its context-less
    /// <see cref="ITool.InvokeAsync(JsonElement, CancellationToken)"/> injects <paramref name="context"/>
    /// (and, for sub-agents, <paramref name="chat"/>). Plain tools pass through unchanged. A null
    /// <paramref name="context"/> is treated as an empty bag.
    /// </summary>
    public static IReadOnlyList<ITool> Bind(
        IReadOnlyList<ITool> tools,
        IRunContext? context,
        IReadOnlyList<ChatMessage>? chat)
    {
        if (tools.Count == 0)
            return tools;

        var bag = context ?? new RunContext();
        var bound = new ITool[tools.Count];
        for (var i = 0; i < tools.Count; i++)
        {
            bound[i] = tools[i] switch
            {
                IAgentTool agentTool => new AgentBoundTool(agentTool, bag, chat),
                IContextualTool contextual => new ContextualBoundTool(contextual, bag),
                var plain => plain,
            };
        }
        return bound;
    }

    /// <summary>
    /// Wraps a contextual tool, injecting the run's context bag on the context-less call path.
    /// Name/description/schema are forwarded so the exposed tool is indistinguishable to the model.
    /// </summary>
    private sealed class ContextualBoundTool(IContextualTool inner, IRunContext context) : ITool
    {
        public string Name => inner.Name;
        public string Description => inner.Description;
        public JsonElement InputSchema => inner.InputSchema;

        public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
            => inner.InvokeAsync(arguments, context, cancellationToken);
    }

    /// <summary>
    /// Wraps a sub-agent tool, forwarding the parent's trusted context bag and seeded chat so the
    /// nested run (and its own tools) behave as on the in-process path.
    /// </summary>
    private sealed class AgentBoundTool(
        IAgentTool inner,
        IRunContext context,
        IReadOnlyList<ChatMessage>? chat) : ITool
    {
        public string Name => inner.Name;
        public string Description => inner.Description;
        public JsonElement InputSchema => inner.InputSchema;

        public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
            => inner.InvokeAsync(arguments, context, chat, cancellationToken);
    }
}

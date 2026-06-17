using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Adapts the framework's tools for an <b>external</b> agent runtime that executes tools itself —
/// primarily the Claude Code CLI (<c>claude -p</c>) reached over the loopback MCP bridge — which can
/// only invoke the context-less <see cref="ITool.InvokeAsync(JsonElement, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// In-process, <see cref="ToolExecutor"/> dispatches scoped tools (<see cref="IScopedTool"/>) with the
/// per-call trusted context and sub-agent tools (<see cref="IAgentTool"/>) with the full context list and
/// seeded chat. An external runtime cannot — so each such tool is wrapped here in a plain
/// <see cref="ITool"/> that closes over the captured context (and chat) and injects it on call, exactly as
/// the in-process executor would. Plain tools are returned unchanged. Name/description/schema are forwarded
/// verbatim, so the model sees an identical tool set; only the invocation path differs.
/// </remarks>
internal static class AgentToolContextBinder
{
    /// <summary>
    /// Returns <paramref name="tools"/> with every scoped/sub-agent tool wrapped so its context-less
    /// <see cref="ITool.InvokeAsync(JsonElement, CancellationToken)"/> injects <paramref name="context"/>
    /// (and, for sub-agents, <paramref name="chat"/>). Plain tools pass through unchanged.
    /// </summary>
    public static IReadOnlyList<ITool> Bind(
        IReadOnlyList<ITool> tools,
        IReadOnlyList<object>? context,
        IReadOnlyList<ChatMessage>? chat)
    {
        if (tools.Count == 0)
            return tools;

        var bound = new ITool[tools.Count];
        for (var i = 0; i < tools.Count; i++)
        {
            bound[i] = tools[i] switch
            {
                IScopedTool scoped => new ScopedBoundTool(scoped, ResolveContext(scoped.ContextType, context)),
                IAgentTool agentTool => new AgentBoundTool(agentTool, context, chat),
                var plain => plain,
            };
        }
        return bound;
    }

    /// <summary>
    /// Mirrors <see cref="ToolExecutor"/>'s context resolution: the exact-type match wins; otherwise the
    /// single value assignable to <paramref name="scopeType"/>. Returns null when none was supplied (the
    /// wrapper then fails the call the same way the in-process path would); throws on ambiguity.
    /// </summary>
    private static object? ResolveContext(Type scopeType, IReadOnlyList<object>? context)
    {
        if (context is null || context.Count == 0)
            return null;

        // Exact type first — unambiguous and the common case.
        foreach (var item in context)
            if (item is not null && item.GetType() == scopeType)
                return item;

        // Then assignable (interface / base-class contexts), guarding against ambiguity.
        object? match = null;
        foreach (var item in context)
        {
            if (item is null || !scopeType.IsInstanceOfType(item))
                continue;
            if (match is not null)
                throw new AiToolContextException(
                    $"Ambiguous context for type '{scopeType.Name}': multiple values passed in " +
                    "'context:' are assignable to it. Pass a single matching value.");
            match = item;
        }
        return match;
    }

    /// <summary>
    /// Wraps a scoped tool, injecting the resolved <c>TScope</c> context on the context-less call path.
    /// Name/description/schema are forwarded so the exposed tool is indistinguishable to the model.
    /// </summary>
    private sealed class ScopedBoundTool(IScopedTool inner, object? context) : ITool
    {
        public string Name => inner.Name;
        public string Description => inner.Description;
        public JsonElement InputSchema => inner.InputSchema;

        public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
        {
            // Same contract as the in-process runner: a missing context is a wiring mistake. Over the
            // bridge it cannot reach the developer, so it surfaces as a tool error to the model.
            if (context is null)
                throw new AiToolContextException(
                    $"Tool '{inner.Name}' requires context of type '{inner.ContextType.Name}', " +
                    "but the agent call supplied no matching value. Pass it via the 'context:' argument.");
            return inner.InvokeAsync(arguments, context, cancellationToken);
        }
    }

    /// <summary>
    /// Wraps a sub-agent tool, forwarding the parent's full trusted context list and seeded chat so the
    /// nested run (and its own scoped tools) behave as on the in-process path.
    /// </summary>
    private sealed class AgentBoundTool(
        IAgentTool inner,
        IReadOnlyList<object>? context,
        IReadOnlyList<ChatMessage>? chat) : ITool
    {
        public string Name => inner.Name;
        public string Description => inner.Description;
        public JsonElement InputSchema => inner.InputSchema;

        public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
            => inner.InvokeAsync(arguments, context, chat, cancellationToken);
    }
}

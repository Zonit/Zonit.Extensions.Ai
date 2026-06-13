using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Bridges a declarative <see cref="IAgent"/> into an <see cref="ITool"/> the parent model can call.
/// On invocation it runs the sub-agent on its own model and tools — forwarding the parent conversation
/// (chat-driven agents) or the model-supplied arguments (parametrized agents) plus the trusted context —
/// and returns the sub-agent's final text as the tool result, ready for the parent to re-voice.
/// </summary>
/// <remarks>
/// The nested run goes through the normal <c>ai.Chat(...).RunAsync()</c> engine, so it attaches to the
/// usage tree under this tool's node (cost/token roll-up) and is bounded by the agent nesting-depth
/// guard — identical to a hand-written tool that injects <see cref="IAiProvider"/> and runs a sub-agent.
/// </remarks>
internal sealed class AgentToolAdapter : IAgentTool
{
    private static readonly JsonElement _emptySchema = BuildEmptySchema();

    private readonly IAgent _agent;
    private readonly IServiceProvider _services;

    public AgentToolAdapter(IAgent agent, IServiceProvider services)
    {
        _agent = agent;
        _services = services;
    }

    public string Name => _agent.Name;

    public string Description => _agent.Description;

    public JsonElement InputSchema => _agent is IInputAgent input ? input.InputSchema : _emptySchema;

    // A sub-agent always needs the ambient chat / context; the runner routes through the
    // IAgentTool overload. This plain path (no context, no chat) is only hit if an agent tool is
    // invoked outside the agent loop — run it with what we have rather than fail.
    Task<JsonElement> ITool.InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
        => InvokeAsync(arguments, context: null, chat: null, cancellationToken);

    public async Task<JsonElement> InvokeAsync(
        JsonElement arguments,
        IReadOnlyList<object>? context,
        IReadOnlyList<ChatMessage>? chat,
        CancellationToken cancellationToken)
    {
        var ai = _services.GetRequiredService<IAiProvider>();

        // System instruction: chat-driven → the Prompt as-is; parametrized → the Scriban Prompt
        // rendered with the model-supplied arguments.
        var system = _agent is IInputAgent
            ? AgentInputTemplate.Render(_agent.Prompt, arguments)
            : _agent.Prompt;

        // Forward the parent conversation whenever there is one and the agent did not opt out. This
        // holds for BOTH modes — a parametrized agent still sees the chat (alongside its rendered task)
        // unless ForwardChat is false. A plain agent-run parent (ai.Agent) has no chat, so the
        // sub-agent runs as a standalone task.
        var result = _agent.ForwardChat && chat is { Count: > 0 }
            ? await RunWithChatAsync(ai, system, chat, context, cancellationToken).ConfigureAwait(false)
            : await RunAsTaskAsync(ai, system, context, cancellationToken).ConfigureAwait(false);

        return JsonString(result.Value ?? string.Empty);
    }

    // Continues the (forwarded) conversation: system instruction + the parent's chat history.
    private async Task<Result<string>> RunWithChatAsync(
        IAiProvider ai, string system, IReadOnlyList<ChatMessage> chat, IReadOnlyList<object>? context, CancellationToken ct)
    {
        var request = ai.Chat(_agent.Llm, system, chat);
        ApplyToolsAndContext(t => request.AddTool(t), c => request.WithContext(c), context);
        return await request.RunAsync(ct).ConfigureAwait(false);
    }

    // Runs the sub-agent as a standalone task (no conversation forwarded).
    private async Task<Result<string>> RunAsTaskAsync(
        IAiProvider ai, string system, IReadOnlyList<object>? context, CancellationToken ct)
    {
        var request = ai.Agent(_agent.Llm, system);
        ApplyToolsAndContext(t => request.AddTool(t), c => request.WithContext(c), context);
        return await request.RunAsync(ct).ConfigureAwait(false);
    }

    private void ApplyToolsAndContext(Action<ITool> addTool, Action<object> withContext, IReadOnlyList<object>? context)
    {
        foreach (var toolType in _agent.Tools)
            addTool((ITool)_services.GetRequiredService(toolType));
        if (context is not null)
            foreach (var item in context)
                withContext(item);
    }

    private static JsonElement JsonString(string value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            writer.WriteStringValue(value);
        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private static JsonElement BuildEmptySchema()
    {
        // Hand-rolled to stay AOT-safe (no reflection-based serialization).
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            writer.WriteEndObject();
            writer.WriteBoolean("additionalProperties", false);
            writer.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }
}

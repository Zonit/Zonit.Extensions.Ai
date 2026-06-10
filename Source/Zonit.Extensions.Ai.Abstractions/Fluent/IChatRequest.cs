using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Fluent builder for a multi-turn chat completion. Obtain one from
/// <see cref="IAiProvider.Chat{TResponse}(ILlm, IPrompt{TResponse}, IReadOnlyList{ChatMessage})"/>;
/// the prompt is the system instruction and the conversation timeline (<c>history</c>) is supplied
/// at the entry point. Terminate with <see cref="RunAsync"/>.
/// </summary>
/// <remarks>
/// Same safe-by-default tooling as <see cref="IAgentRequest{TResponse}"/>: no tools or MCP servers
/// are exposed unless added explicitly or opted in via <see cref="AddDefaultTools"/> /
/// <see cref="AddDefaultMcp"/>. Trusted server data flows through <see cref="WithContext"/>.
/// </remarks>
public interface IChatRequest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>
{
    /// <summary>Adds a tool resolved from DI (its dependencies are injected by the container).</summary>
    IChatRequest<TResponse> AddTool<TTool>() where TTool : class, ITool;

    /// <summary>Adds an already-constructed tool instance.</summary>
    IChatRequest<TResponse> AddTool(ITool tool);

    /// <summary>Adds several already-constructed tool instances.</summary>
    IChatRequest<TResponse> AddTools(IEnumerable<ITool> tools);

    /// <summary>Opts this call into the globally registered tool set (<c>AddAiTools&lt;T&gt;()</c>). Off unless called.</summary>
    IChatRequest<TResponse> AddDefaultTools();

    /// <summary>Attaches an MCP server for this call, with optional per-server configuration.</summary>
    IChatRequest<TResponse> AddMcp(string name, string url, string? token = null, Action<IMcpOptions>? configure = null);

    /// <summary>Opts this call into the globally registered MCP servers (<c>AddAiMcp(...)</c>). Off unless called.</summary>
    IChatRequest<TResponse> AddDefaultMcp();

    /// <summary>Supplies trusted server data delivered to scoped tools by <c>TScope</c> type, never sent to the model.</summary>
    IChatRequest<TResponse> WithContext(object context);

    /// <summary>Restricts the model to tools whose name appears in <paramref name="toolNames"/>.</summary>
    IChatRequest<TResponse> AllowOnly(params string[] toolNames);

    /// <summary>Gate run before each tool execution; returning <c>false</c> blocks that call.</summary>
    IChatRequest<TResponse> OnToolCall(Func<ToolInvocation, CancellationToken, ValueTask<bool>> handler);

    /// <summary>Hard ceiling on tool-loop iterations for this turn.</summary>
    IChatRequest<TResponse> MaxIterations(int max);

    /// <summary>Concurrency for tool execution within a turn (surplus is queued, never dropped).</summary>
    IChatRequest<TResponse> MaxParallelToolCalls(int max);

    /// <summary>Wall-clock timeout for the whole turn.</summary>
    IChatRequest<TResponse> Timeout(TimeSpan timeout);

    /// <summary>Runs the chat turn and returns the result. When tools ran, the value is a <see cref="ResultAgent{TResponse}"/>.</summary>
    Task<Result<TResponse>> RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the chat turn and streams <see cref="AgentEvent"/>s (tool activity + final). Requires an
    /// agent-capable model (<see cref="IAgentLlm"/>); for plain token-by-token streaming without tools
    /// use <c>ai.ChatStreamAsync(llm, system, history)</c>.
    /// </summary>
    IAsyncEnumerable<AgentEvent> RunStreamAsync(CancellationToken cancellationToken = default);
}

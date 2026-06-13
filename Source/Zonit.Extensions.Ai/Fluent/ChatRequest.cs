using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>Invokes the underlying chat call once the fluent builder is terminated.</summary>
internal delegate Task<Result<TResponse>> ChatRunInvoker<TResponse>(
    IReadOnlyList<ITool>? tools, IReadOnlyList<Mcp>? mcps, AgentOptions? options,
    IReadOnlyList<object>? context, CancellationToken cancellationToken);

/// <summary>Invokes the underlying streaming chat call once the fluent builder is terminated.</summary>
internal delegate IAsyncEnumerable<AgentEvent> ChatStreamInvoker(
    IReadOnlyList<ITool>? tools, IReadOnlyList<Mcp>? mcps, AgentOptions? options,
    IReadOnlyList<object>? context, CancellationToken cancellationToken);

/// <summary>
/// Default <see cref="IChatRequest{TResponse}"/> implementation: a safe-by-default fluent layer over
/// <see cref="IAiProvider"/>'s chat engine. The bound <paramref name="run"/> / <paramref name="stream"/>
/// delegates capture the model, system prompt and history supplied at the entry point.
/// </summary>
internal sealed class ChatRequest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
    IServiceProvider serviceProvider,
    ChatRunInvoker<TResponse> run,
    ChatStreamInvoker stream) : IChatRequest<TResponse>
{
    private readonly FluentRequestState _state = new(serviceProvider);

    public IChatRequest<TResponse> AddTool<TTool>() where TTool : class, ITool { _state.AddToolFromDi<TTool>(); return this; }
    public IChatRequest<TResponse> AddTool(ITool tool) { _state.Tools.Add(tool); return this; }
    public IChatRequest<TResponse> AddTools(IEnumerable<ITool> tools) { _state.Tools.AddRange(tools); return this; }
    public IChatRequest<TResponse> AddDefaultTools() { _state.AddDefaultTools(); return this; }
    public IChatRequest<TResponse> AddAgent<TAgent>() where TAgent : class, IAgent { _state.AddAgentFromDi<TAgent>(); return this; }
    public IChatRequest<TResponse> AddMcp(string name, string url, string? token = null, Action<IMcpOptions>? configure = null) { _state.AddMcp(name, url, token, configure); return this; }
    public IChatRequest<TResponse> AddDefaultMcp() { _state.AddDefaultMcp(); return this; }
    public IChatRequest<TResponse> WithContext(object context) { _state.Context.Add(context); return this; }
    public IChatRequest<TResponse> AllowOnly(params string[] toolNames) { _state.AllowOnly(toolNames); return this; }
    public IChatRequest<TResponse> OnToolCall(Func<ToolInvocation, CancellationToken, ValueTask<bool>> handler) { _state.OnToolCall(handler); return this; }
    public IChatRequest<TResponse> MaxIterations(int max) { _state.MaxIterations(max); return this; }
    public IChatRequest<TResponse> MaxParallelToolCalls(int max) { _state.MaxParallelToolCalls(max); return this; }
    public IChatRequest<TResponse> Timeout(TimeSpan timeout) { _state.Timeout(timeout); return this; }

    public Task<Result<TResponse>> RunAsync(CancellationToken cancellationToken = default)
        => run(_state.ToolsOrNull(), _state.McpsOrNull(), _state.OptionsOrNull(), _state.ContextOrNull(), cancellationToken);

    public IAsyncEnumerable<AgentEvent> RunStreamAsync(CancellationToken cancellationToken = default)
        => stream(_state.ToolsOrNull(), _state.McpsOrNull(), _state.OptionsOrNull(), _state.ContextOrNull(), cancellationToken);
}

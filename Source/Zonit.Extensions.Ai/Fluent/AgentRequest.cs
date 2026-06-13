using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>Invokes the underlying agent call once the fluent builder is terminated.</summary>
internal delegate Task<ResultAgent<TResponse>> AgentRunInvoker<TResponse>(
    IReadOnlyList<ITool>? tools, IReadOnlyList<Mcp>? mcps, AgentOptions? options,
    IReadOnlyList<object>? context, CancellationToken cancellationToken);

/// <summary>Invokes the underlying streaming agent call once the fluent builder is terminated.</summary>
internal delegate IAsyncEnumerable<AgentEvent> AgentStreamInvoker(
    IReadOnlyList<ITool>? tools, IReadOnlyList<Mcp>? mcps, AgentOptions? options,
    IReadOnlyList<object>? context, CancellationToken cancellationToken);

/// <summary>
/// Default <see cref="IAgentRequest{TResponse}"/> implementation. A thin, safe-by-default layer over
/// <see cref="IAiProvider"/>'s agent engine: it accumulates configuration and, at the terminal call,
/// passes an explicit tool/MCP list (no silent DI defaults). The bound <paramref name="run"/> and
/// <paramref name="stream"/> delegates capture the model and prompt supplied at the entry point.
/// </summary>
internal sealed class AgentRequest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
    IServiceProvider serviceProvider,
    AgentRunInvoker<TResponse> run,
    AgentStreamInvoker stream) : IAgentRequest<TResponse>
{
    private readonly FluentRequestState _state = new(serviceProvider);

    public IAgentRequest<TResponse> AddTool<TTool>() where TTool : class, ITool { _state.AddToolFromDi<TTool>(); return this; }
    public IAgentRequest<TResponse> AddTool(ITool tool) { _state.Tools.Add(tool); return this; }
    public IAgentRequest<TResponse> AddTools(IEnumerable<ITool> tools) { _state.Tools.AddRange(tools); return this; }
    public IAgentRequest<TResponse> AddDefaultTools() { _state.AddDefaultTools(); return this; }
    public IAgentRequest<TResponse> AddAgent<TAgent>() where TAgent : class, IAgent { _state.AddAgentFromDi<TAgent>(); return this; }
    public IAgentRequest<TResponse> AddMcp(string name, string url, string? token = null, Action<IMcpOptions>? configure = null) { _state.AddMcp(name, url, token, configure); return this; }
    public IAgentRequest<TResponse> AddDefaultMcp() { _state.AddDefaultMcp(); return this; }
    public IAgentRequest<TResponse> WithContext(object context) { _state.Context.Add(context); return this; }
    public IAgentRequest<TResponse> AllowOnly(params string[] toolNames) { _state.AllowOnly(toolNames); return this; }
    public IAgentRequest<TResponse> OnToolCall(Func<ToolInvocation, CancellationToken, ValueTask<bool>> handler) { _state.OnToolCall(handler); return this; }
    public IAgentRequest<TResponse> MaxIterations(int max) { _state.MaxIterations(max); return this; }
    public IAgentRequest<TResponse> MaxParallelToolCalls(int max) { _state.MaxParallelToolCalls(max); return this; }
    public IAgentRequest<TResponse> Timeout(TimeSpan timeout) { _state.Timeout(timeout); return this; }
    public IAgentRequest<TResponse> MaxNestedDepth(int depth) { _state.MaxNestedDepth(depth); return this; }

    public Task<ResultAgent<TResponse>> RunAsync(CancellationToken cancellationToken = default)
        => run(_state.ToolsOrNull(), _state.McpsOrNull(), _state.OptionsOrNull(), _state.ContextOrNull(), cancellationToken);

    public IAsyncEnumerable<AgentEvent> RunStreamAsync(CancellationToken cancellationToken = default)
        => stream(_state.ToolsOrNull(), _state.McpsOrNull(), _state.OptionsOrNull(), _state.ContextOrNull(), cancellationToken);
}

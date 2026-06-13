using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Shared mutable accumulator behind the fluent request builders
/// (<see cref="AgentRequest{T}"/>, <see cref="ChatRequest{T}"/>). Keeps tool / MCP / context lists
/// and per-call option overrides; resolves DI-backed tools and the globally registered sets so the
/// terminal call always passes an authoritative, explicit list (defaults stay off unless opted in).
/// </summary>
internal sealed class FluentRequestState(IServiceProvider serviceProvider)
{
    public readonly List<ITool> Tools = [];
    public readonly List<Mcp> Mcps = [];
    public readonly List<object> Context = [];

    private int? _maxIterations;
    private int? _maxParallel;
    private int? _maxNestedDepth;
    private TimeSpan? _timeout;
    private List<string>? _allowed;
    private Func<ToolInvocation, CancellationToken, ValueTask<bool>>? _onToolCall;
    private bool _hasOverride;

    public void AddToolFromDi<TTool>() where TTool : class, ITool
        => Tools.Add(serviceProvider.GetRequiredService<TTool>());

    public void AddAgentFromDi<TAgent>() where TAgent : class, IAgent
        => Tools.Add(new AgentToolAdapter(serviceProvider.GetRequiredService<TAgent>(), serviceProvider));

    public void AddDefaultTools()
    {
        var registry = serviceProvider.GetService<IToolRegistry>();
        if (registry is not null)
            Tools.AddRange(registry.GetAll());
    }

    public void AddDefaultMcp()
    {
        var registry = serviceProvider.GetService<IMcpRegistry>();
        if (registry is not null)
            Mcps.AddRange(registry.GetAll());
    }

    public void AddMcp(string name, string url, string? token, Action<IMcpOptions>? configure)
    {
        string[]? allowed = null;
        if (configure is not null)
        {
            var options = new McpOptions();
            configure(options);
            allowed = options.Allowed;
        }

        Mcps.Add(new Mcp(name, url, token, allowed));
    }

    public void AllowOnly(string[] toolNames) { (_allowed ??= []).AddRange(toolNames); _hasOverride = true; }
    public void OnToolCall(Func<ToolInvocation, CancellationToken, ValueTask<bool>> handler) { _onToolCall = handler; _hasOverride = true; }
    public void MaxIterations(int max) { _maxIterations = max; _hasOverride = true; }
    public void MaxParallelToolCalls(int max) { _maxParallel = max; _hasOverride = true; }
    public void MaxNestedDepth(int depth) { _maxNestedDepth = depth; _hasOverride = true; }
    public void Timeout(TimeSpan timeout) { _timeout = timeout; _hasOverride = true; }

    /// <summary>Tools as an explicit list, or <c>null</c> when none were added (lets a plain chat skip the agent path).</summary>
    public IReadOnlyList<ITool>? ToolsOrNull() => Tools.Count == 0 ? null : Tools;

    /// <summary>MCP servers as an explicit list, or <c>null</c> when none were added.</summary>
    public IReadOnlyList<Mcp>? McpsOrNull() => Mcps.Count == 0 ? null : Mcps;

    /// <summary>Context as an explicit list, or <c>null</c> when none was added.</summary>
    public IReadOnlyList<object>? ContextOrNull() => Context.Count == 0 ? null : Context;

    /// <summary>
    /// Built options, or <c>null</c> when nothing was overridden — so a plain chat (no tools, no
    /// limits) routes to the single-shot provider path instead of the agent runner.
    /// </summary>
    public AgentOptions? OptionsOrNull() => _hasOverride ? BuildOptions() : null;

    /// <summary>
    /// Builds the per-call options. <c>DefaultTools</c> / <c>DefaultMcp</c> stay <c>false</c>: the
    /// builder has already materialized any opted-in defaults into <see cref="Tools"/> /
    /// <see cref="Mcps"/>, so the terminal call passes an explicit, authoritative list.
    /// </summary>
    public AgentOptions BuildOptions() => new()
    {
        DefaultTools = false,
        DefaultMcp = false,
        MaxIterations = _maxIterations,
        MaxParallelToolCalls = _maxParallel,
        MaxNestedDepth = _maxNestedDepth,
        Timeout = _timeout,
        AllowedTools = _allowed,
        OnToolCall = _onToolCall,
    };
}

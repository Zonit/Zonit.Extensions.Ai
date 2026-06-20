using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Core agent loop. Resolves tools, picks a provider adapter, drives the
/// model round-trip loop, executes tool calls in parallel and aggregates
/// per-turn usage / cost into a single <see cref="ResultAgent{TResponse}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StreamAsync{TResponse}"/> is the primary entry point and emits
/// a structured event stream (<see cref="AgentEvent"/>). <see cref="RunAsync{TResponse}"/>
/// is a thin wrapper that collects events and returns the terminal result —
/// or re-throws the captured exception on failure.
/// </para>
/// <para>
/// Fallback precedence for <see cref="AgentOptions.MaxIterations"/>:
/// per-call option → global <c>AiAgentOptions.MaxIterations</c> →
/// model's <see cref="IAgentLlm.DefaultMaxIterations"/>.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("JSON serialization requires types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization requires runtime code generation.")]
internal sealed class AgentRunner
{
    private readonly IEnumerable<IAgentProviderAdapter> _adapters;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMcpToolFactory _mcpFactory;
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly AiUsageTracker _tracker;
    private readonly ILogger<AgentRunner> _logger;

    public AgentRunner(
        IEnumerable<IAgentProviderAdapter> adapters,
        IServiceScopeFactory scopeFactory,
        IMcpToolFactory mcpFactory,
        IOptions<AiOptions> aiOptions,
        AiUsageTracker tracker,
        ILogger<AgentRunner> logger)
    {
        _adapters = adapters;
        _scopeFactory = scopeFactory;
        _mcpFactory = mcpFactory;
        _aiOptions = aiOptions;
        _tracker = tracker;
        _logger = logger;
    }

    /// <summary>
    /// Runs the agent loop to completion and returns the terminal result.
    /// Re-throws <see cref="AgentException"/> / <see cref="OperationCanceledException"/>
    /// / provider exceptions when the run fails.
    /// </summary>
    public async Task<ResultAgent<TResponse>> RunAsync<TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? callerTools,
        IReadOnlyList<Mcp>? callerMcps,
        AgentOptions? options,
        CancellationToken cancellationToken,
        IReadOnlyList<ChatMessage>? initialChat = null,
        IReadOnlyList<object>? callerContext = null)
    {
        await foreach (var evt in StreamAsync(llm, prompt, callerTools, callerMcps, options, cancellationToken, initialChat, callerContext)
                           .ConfigureAwait(false))
        {
            switch (evt)
            {
                case AgentCompletedEvent<TResponse> completed:
                    return completed.Result;

                case AgentFailedEvent failed:
                    // Preserve original stack / AgentException semantics.
                    throw failed.Error;
            }
        }

        throw new InvalidOperationException("Agent stream ended without a terminal event.");
    }

    /// <summary>
    /// Streams the agent loop as a sequence of <see cref="AgentEvent"/>s.
    /// The stream always ends with either <see cref="AgentCompletedEvent{TResponse}"/>
    /// (success) or <see cref="AgentFailedEvent"/> (failure) — never both.
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> StreamAsync<TResponse>(
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? callerTools,
        IReadOnlyList<Mcp>? callerMcps,
        AgentOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        IReadOnlyList<ChatMessage>? initialChat = null,
        IReadOnlyList<object>? callerContext = null)
    {
        // Open the usage-tracking node for this run BEFORE the core iterator, so the
        // restore in the finally runs on EVERY exit path (yield break, exception, or
        // an abandoned enumerator — DisposeAsync runs the finally). The node stays
        // current for the whole loop, so any AI call a tool makes attaches here.
        // BeginScope enforces the nesting-depth guard and may throw
        // AiNestingLimitException, which surfaces to the caller / enclosing tool.
        var globalAgent = _aiOptions.Value.Agent ?? new AiAgentOptions();
        var maxNestedDepth = options?.MaxNestedDepth ?? globalAgent.MaxNestedDepth;
        var captureIo = globalAgent.CaptureNestedIo;

        var scope = _tracker.BeginScope(AiUsageKind.Agent, llm.Name, provider: null, maxDepth: maxNestedDepth);
        try
        {
            await foreach (var evt in StreamCoreAsync<TResponse>(
                    scope, captureIo, llm, prompt, callerTools, callerMcps, options, cancellationToken, initialChat, callerContext)
                .ConfigureAwait(false))
            {
                yield return evt;
            }
        }
        finally
        {
            _tracker.EndScope(scope);
        }
    }

    private async IAsyncEnumerable<AgentEvent> StreamCoreAsync<TResponse>(
        AiUsageScopeBuilder scope,
        bool captureIo,
        IAgentLlm llm,
        IPrompt<TResponse> prompt,
        IReadOnlyList<ITool>? callerTools,
        IReadOnlyList<Mcp>? callerMcps,
        AgentOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        IReadOnlyList<ChatMessage>? initialChat = null,
        IReadOnlyList<object>? callerContext = null)
    {
        var globalAgent = _aiOptions.Value.Agent ?? new AiAgentOptions();
        // Precedence: per-call option → model's DefaultMaxIterations (if < global) → global cap.
        // The global value acts as an absolute safety cap the model cannot exceed unless the
        // caller opts in explicitly on a per-call basis.
        var maxIterations = options?.MaxIterations
                         ?? Math.Min(llm.DefaultMaxIterations, globalAgent.MaxIterations);
        var maxParallel = options?.MaxParallelToolCalls ?? globalAgent.MaxParallelToolCalls;
        var perCallTimeout = globalAgent.ToolCallTimeout;
        var exceptionPolicy = globalAgent.OnToolException;

        // Combined cancellation: caller token + optional per-call wall clock.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options?.Timeout is { } t && t > TimeSpan.Zero)
            cts.CancelAfter(t);
        var runToken = cts.Token;

        // Scope lives as long as the agent run — scoped ITool instances resolved
        // from the registry (and their scoped dependencies like DbContext) remain
        // valid for the entire loop and are disposed at the end.
        await using var runScope = _scopeFactory.CreateAsyncScope();

        // Build the trusted context bag once for the whole run, from the caller's WithContext(...)
        // values. The SAME instance is shared by every tool (so in-place mutations and Set<T> are
        // visible to later tools and the host), forwarded to sub-agents, and used to gate tool
        // visibility below.
        var aiContext = new RunContext(callerContext);

        // Seed framework-owned facts about this run's conversation so tools and sub-agents can read
        // them like any other context value (context.Get<ConversationInfo>()). Set<T> overwrites by
        // type, so a sub-agent run replaces the forwarded parent value with its own. The count is
        // known here, before ResolveToolsAsync evaluates IsAvailable, so it can gate sub-agents
        // (e.g. an "opener" shown only on an empty conversation).
        aiContext.Set(new ConversationInfo { MessageCount = initialChat?.Count ?? 0 });

        // 1. Resolve tools (caller-provided + DI registry + MCP-exposed), then drop any whose
        //    IsAvailable(context) gate is unmet — evaluated once here, so the tool set is fixed for
        //    the run (a tool cannot be removed mid-turn); the next run re-evaluates a changed context.
        var resolvedTools = await ResolveToolsAsync(runScope.ServiceProvider, callerTools, callerMcps, options, aiContext, runToken).ConfigureAwait(false);

        // 2. Pick a provider adapter.
        var adapter = _adapters.FirstOrDefault(a => a.SupportsAgent(llm))
            ?? throw new InvalidOperationException(
                $"No IAgentProviderAdapter registered for model {llm.GetType().FullName}. "
                + "Ensure the provider's AddAi<Provider>() extension is called.");

        // The adapter is the "provider" for this agent node (model name was set when
        // the scope was opened; the adapter is only known now).
        scope.Provider = adapter.GetType().Name;

        // 3. Open the session.
        var context = new AgentSessionContext
        {
            Llm = llm,
            Prompt = prompt,
            ResponseType = typeof(TResponse) == typeof(string) ? null : typeof(TResponse),
            Tools = resolvedTools,
            InitialChat = initialChat,
            // Forwarded so transports whose external runtime executes tools (the Claude Code CLI
            // over the MCP bridge) can bind it onto contextual/sub-agent tools. The in-process
            // ToolExecutor below shares the same bag for the HTTP path.
            Context = aiContext,
        };

        await using var session = adapter.BeginSession(context);

        var executor = new ToolExecutor(
            resolvedTools,
            aiContext,
            initialChat,
            maxParallel,
            perCallTimeout,
            exceptionPolicy,
            options?.OnToolCall,
            _tracker,
            scope,
            _logger);

        var invocations = new List<ToolInvocation>();
        var totalUsage = new TokenUsage();
        var totalCost = Price.Zero;
        IReadOnlyList<ToolResult>? pendingResults = null;
        var iteration = 0;
        var finalText = string.Empty;
        TokenUsage lastUsage = new();
        TimeSpan lastDuration = TimeSpan.Zero;
        string? lastRequestId = null;

        while (true)
        {
            runToken.ThrowIfCancellationRequested();

            iteration++;
            if (iteration > maxIterations)
            {
                var partial = BuildPartial(iteration - 1, invocations, totalUsage, totalCost);
                var ex = new AgentIterationLimitException(maxIterations, partial);
                yield return new AgentFailedEvent
                {
                    Iteration = iteration - 1,
                    Timestamp = DateTimeOffset.UtcNow,
                    Error = ex,
                    Partial = partial,
                };
                yield break;
            }

            yield return new AgentIterationStartedEvent
            {
                Iteration = iteration,
                Timestamp = DateTimeOffset.UtcNow,
            };

            AgentTurn? turn = null;
            Exception? turnFailure = null;
            AgentPartialResult? turnFailurePartial = null;
            try
            {
                turn = await session.RunTurnAsync(pendingResults, runToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Timeout (our linked token fired but caller's didn't).
                turnFailurePartial = BuildPartial(iteration - 1, invocations, totalUsage, totalCost);
                turnFailure = new AgentException($"Agent timed out after {options!.Timeout}.", turnFailurePartial);
            }
            catch (Exception ex)
            {
                turnFailurePartial = BuildPartial(iteration - 1, invocations, totalUsage, totalCost);
                turnFailure = ex;
            }

            if (turnFailure is not null)
            {
                yield return new AgentFailedEvent
                {
                    Iteration = iteration,
                    Timestamp = DateTimeOffset.UtcNow,
                    Error = turnFailure,
                    Partial = turnFailurePartial,
                };
                yield break;
            }

            // turn is guaranteed non-null here: turnFailure was null, so RunTurnAsync returned successfully.
            System.Diagnostics.Debug.Assert(turn is not null);
            totalUsage = Sum(totalUsage, turn!.Usage);
            totalCost = totalCost + turn.Usage.InputCost + turn.Usage.OutputCost;
            lastUsage = turn.Usage;
            lastDuration = turn.Duration;
            lastRequestId = turn.RequestId;

            // Record this turn into the agent's own usage node (single-writer: the
            // loop is sequential). The prompt is captured once, on the first turn.
            _tracker.Record(scope, turn.Usage, turn.Duration, turn.RequestId,
                input: iteration == 1 && captureIo ? UsageText.Preview(prompt.Text) : null);

            yield return new AgentTurnCompletedEvent
            {
                Iteration = iteration,
                Timestamp = DateTimeOffset.UtcNow,
                Usage = turn.Usage,
                Duration = turn.Duration,
                ToolCallCount = turn.ToolCalls.Count,
                RequestId = turn.RequestId,
            };

            // Terminal: model produced a final answer.
            if (turn.ToolCalls.Count == 0)
            {
                finalText = turn.FinalText ?? string.Empty;
                if (captureIo)
                    _tracker.SetOutput(scope, UsageText.Preview(finalText));
                yield return new AgentFinalTextEvent
                {
                    Iteration = iteration,
                    Timestamp = DateTimeOffset.UtcNow,
                    Text = finalText,
                };
                break;
            }

            // Notify observers that a batch of tool calls is about to execute.
            foreach (var call in turn.ToolCalls)
            {
                yield return new AgentToolCallStartedEvent
                {
                    Iteration = iteration,
                    Timestamp = DateTimeOffset.UtcNow,
                    ToolName = call.Name,
                    CallId = call.Id,
                };
            }

            // Execute the batch. Any ThrowToCaller exception surfaces here.
            IReadOnlyList<ToolResult>? toolResults = null;
            var batchSink = new List<ToolInvocation>();
            Exception? batchFailure = null;
            try
            {
                toolResults = await executor.ExecuteAsync(iteration, turn.ToolCalls, batchSink, runToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                batchFailure = ex;
            }

            invocations.AddRange(batchSink);
            foreach (var inv in batchSink)
            {
                yield return new AgentToolCallCompletedEvent
                {
                    Iteration = iteration,
                    Timestamp = DateTimeOffset.UtcNow,
                    Invocation = inv,
                };
            }

            if (batchFailure is not null)
            {
                var partial = BuildPartial(iteration, invocations, totalUsage, totalCost);
                yield return new AgentFailedEvent
                {
                    Iteration = iteration,
                    Timestamp = DateTimeOffset.UtcNow,
                    Error = batchFailure,
                    Partial = partial,
                };
                yield break;
            }

            pendingResults = toolResults;
        }

        // 4. Terminal: parse final text into TResponse and emit Completed.
        ResultAgent<TResponse>? result = null;
        Exception? parseFailure = null;
        try
        {
            var value = ParseFinal<TResponse>(finalText, prompt);
            var metadata = new MetaData
            {
                Model = llm,
                Provider = adapter.GetType().Name,
                PromptName = PromptNameResolver.Resolve(prompt),
                Usage = lastUsage,
                Duration = lastDuration,
                RequestId = lastRequestId,
            };
            // Freeze the call tree once; expose it as the hierarchy (Usage), a flat list
            // of nested model calls (NestedAiCalls), and two roll-ups: Request (this
            // agent's own turns) and Total (everything, including nested tool/sub-agent AI).
            var usageTree = scope.Freeze();
            var request = new AiUsageSummary
            {
                Tokens = usageTree.Usage,
                Duration = usageTree.Duration,
                Calls = usageTree.Calls,
            };
            var total = new AiUsageSummary
            {
                Tokens = usageTree.TotalUsage,
                Duration = usageTree.TotalDuration,
                Calls = usageTree.TotalCalls,
            };
            result = new ResultAgent<TResponse>
            {
                Value = value,
                MetaData = metadata,
                Iterations = iteration,
                ToolCalls = invocations,
                Usage = usageTree,
                NestedAiCalls = usageTree.ModelCalls(),
                Request = request,
                Total = total,
            };
        }
        catch (Exception ex)
        {
            parseFailure = ex;
        }

        if (parseFailure is not null)
        {
            var partial = BuildPartial(iteration, invocations, totalUsage, totalCost);
            yield return new AgentFailedEvent
            {
                Iteration = iteration,
                Timestamp = DateTimeOffset.UtcNow,
                Error = new AgentException(
                    $"Failed to parse the model's final answer as {typeof(TResponse).Name}: {parseFailure.Message}",
                    parseFailure,
                    partial),
                Partial = partial,
            };
            yield break;
        }

        yield return new AgentCompletedEvent<TResponse>
        {
            Iteration = iteration,
            Timestamp = DateTimeOffset.UtcNow,
            Result = result!,
        };
    }

    private async Task<IReadOnlyList<ITool>> ResolveToolsAsync(
        IServiceProvider sp,
        IReadOnlyList<ITool>? callerTools,
        IReadOnlyList<Mcp>? callerMcps,
        AgentOptions? options,
        IRunContext context,
        CancellationToken cancellationToken)
    {
        // DI defaults are applied ONLY when the caller didn't express an
        // opinion (passed null). Passing tools: [] / mcps: [] is the explicit
        // "no tools, no MCPs" signal and must NOT be silently augmented with
        // whatever is registered globally. The same pattern applies to
        // tools: [t1] — the caller wants exactly t1 for this call, not "t1
        // plus everything else in the container".
        //
        // Per-call AgentOptions.DefaultTools / DefaultMcp opt the call INTO the
        // globally registered set when the caller passed null. Default is
        // FALSE — globally registered tools/MCPs are never silently active;
        // they must be opted into per call (AgentOptions.DefaultTools = true,
        // or the fluent .AddDefaultTools() / .AddDefaultMcp()). This keeps a
        // tool registered via AddAiTools<T>() for one flow from leaking into
        // every other agent call in the process.
        var includeDefaultTools = options?.DefaultTools ?? false;
        var includeDefaultMcp = options?.DefaultMcp ?? false;

        var tools = new List<ITool>();

        if (callerTools is not null)
        {
            // Explicit list — authoritative. DI defaults do NOT merge in,
            // even when DefaultTools=true. Pass null to opt into DI defaults.
            tools.AddRange(callerTools);
        }
        else if (includeDefaultTools)
        {
            var toolRegistry = sp.GetService<IToolRegistry>();
            if (toolRegistry is not null)
                tools.AddRange(toolRegistry.GetAll());
        }

        // MCP servers follow the identical authoritative-list semantic.
        var servers = new List<Mcp>();
        if (callerMcps is not null)
        {
            servers.AddRange(callerMcps);
        }
        else if (includeDefaultMcp)
        {
            var mcpRegistry = sp.GetService<IMcpRegistry>();
            if (mcpRegistry is not null) servers.AddRange(mcpRegistry.GetAll());
        }

        if (servers.Count > 0)
        {
            var mcpTools = await _mcpFactory.BuildAsync(servers, cancellationToken).ConfigureAwait(false);
            tools.AddRange(mcpTools);
        }

        // Deduplicate by name (first occurrence wins — caller > registry > MCP).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new List<ITool>(tools.Count);
        foreach (var t in tools)
        {
            if (seen.Add(t.Name)) deduped.Add(t);
            else _logger.LogWarning("Agent tool '{Tool}' is registered twice; keeping first occurrence.", t.Name);
        }

        // Apply allow-list filter.
        if (options?.AllowedTools is { Count: > 0 } allowed)
        {
            var allowSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            deduped = deduped.Where(t => allowSet.Contains(t.Name)).ToList();
        }

        // Visibility gate: a conditional tool (a sub-agent with an IsAvailable override) is exposed
        // only when the trusted context satisfies its predicate. Evaluated once per run.
        var visible = new List<ITool>(deduped.Count);
        foreach (var t in deduped)
        {
            if (t is IConditionalTool conditional && !conditional.IsAvailable(context))
            {
                _logger.LogDebug("Agent tool '{Tool}' hidden by IsAvailable(context).", t.Name);
                continue;
            }
            visible.Add(t);
        }

        return visible;
    }

    private static AgentPartialResult BuildPartial(
        int iteration,
        IReadOnlyList<ToolInvocation> invocations,
        TokenUsage agentUsage,
        Price agentCost)
    {
        // agentUsage/agentCost cover only the agent's own turns. Fold in the AI a
        // tool spent internally (sub-agents / nested model calls) — captured per
        // invocation as NestedUsage — so a partial (failure/timeout) result reports
        // a true end-to-end total, consistent with the success path's ResultAgent.Total.
        var totalUsage = agentUsage;
        var totalCost = agentCost;
        foreach (var inv in invocations)
        {
            if (inv.NestedUsage is { } nested)
            {
                totalUsage = Sum(totalUsage, nested);
                totalCost += nested.TotalCost;
            }
        }

        return new()
        {
            Iterations = iteration,
            ToolCalls = invocations.ToList(),
            TotalUsage = totalUsage,
            TotalCost = totalCost,
        };
    }

    private static TokenUsage Sum(TokenUsage a, TokenUsage b) => TokenUsageMath.Add(a, b);

    [RequiresUnreferencedCode("Falls back to reflection-based JSON deserialization when no AOT binding is registered for TResponse.")]
    [RequiresDynamicCode("Reflection-based JSON deserialization may require runtime code generation.")]
    private static TResponse ParseFinal<TResponse>(string finalText, IPrompt<TResponse> prompt)
    {
        if (typeof(TResponse) == typeof(string))
            return (TResponse)(object)finalText;

        // Routes through AiJsonTypeInfoResolver (AOT-safe) with a reflection
        // fallback for types that weren't picked up by the source generator.
        return JsonResponseParser.Parse<TResponse>(finalText);
    }
}

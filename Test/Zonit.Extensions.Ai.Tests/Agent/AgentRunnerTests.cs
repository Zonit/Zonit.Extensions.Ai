using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Tests.Agent;

/// <summary>
/// End-to-end agent loop tests with a fully in-memory fake provider adapter —
/// exercises tool routing, parallel execution, iteration / cost budgets,
/// structured-output parsing and the streaming <see cref="AgentEvent"/> API.
/// </summary>
public class AgentRunnerTests
{
    #region Fake model / adapter

    private sealed class FakeModel : IAgentLlm, ITextLlm
    {
        public string Name => "fake-agent";
        public int MaxTokens => 1024;
        public decimal PriceInput => 0;
        public decimal PriceOutput => 0;
        public decimal? BatchPriceInput => null;
        public decimal? BatchPriceOutput => null;
        public decimal? PriceCachedInput => null;
        public int MaxInputTokens => 1024;
        public int MaxOutputTokens => 1024;
        public ChannelType Input => ChannelType.Text;
        public ChannelType Output => ChannelType.Text;
        public ToolsType SupportedTools => ToolsType.None;
        public FeaturesType SupportedFeatures => FeaturesType.None;
        public EndpointsType SupportedEndpoints => EndpointsType.None;
        public decimal GetInputPrice(long tokenCount) => 0;
        public decimal GetOutputPrice(long tokenCount) => 0;
        public IToolBase[]? Tools => null;
        public int DefaultMaxIterations => 100;
        public double Temperature { get; set; } = 1.0;
        public double TopP { get; set; } = 1.0;
    }

    /// <summary>
    /// Scripted adapter: each call to <c>RunTurnAsync</c> dequeues the next
    /// scripted turn. Exposes <see cref="SessionsCreated"/> for assertions.
    /// </summary>
    private sealed class ScriptedAgentAdapter : IAgentProviderAdapter
    {
        public Queue<AgentTurn> Turns { get; } = new();
        public List<AgentSessionContext> SessionsCreated { get; } = new();

        public bool SupportsAgent(ILlm llm) => llm is FakeModel;

        public IAgentSession BeginSession(AgentSessionContext context)
        {
            SessionsCreated.Add(context);
            return new ScriptedSession(Turns);
        }

        private sealed class ScriptedSession : IAgentSession
        {
            private readonly Queue<AgentTurn> _turns;
            public ScriptedSession(Queue<AgentTurn> turns) => _turns = turns;

            public Task<AgentTurn> RunTurnAsync(IReadOnlyList<ToolResult>? toolResults, CancellationToken cancellationToken)
            {
                if (_turns.Count == 0)
                    throw new InvalidOperationException("No more scripted turns.");
                return Task.FromResult(_turns.Dequeue());
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class EchoTool : ToolBase<EchoTool.Input, EchoTool.Output>
    {
        public override string Name => "echo";
        public override string Description => "Echoes the input.";

        public override Task<Output> ExecuteAsync(Input input, CancellationToken cancellationToken)
            => Task.FromResult(new Output { Echo = input.Message });

        public class Input { public required string Message { get; init; } }
        public class Output { public required string Echo { get; init; } }
    }

    private sealed class ThrowingTool : ToolBase<ThrowingTool.Input, ThrowingTool.Output>
    {
        public override string Name => "explode";
        public override string Description => "Always throws.";

        public override Task<Output> ExecuteAsync(Input input, CancellationToken cancellationToken)
            => throw new InvalidOperationException("planned failure");

        public class Input { public string? Ignored { get; init; } }
        public class Output { public bool Ok { get; init; } }
    }

    /// <summary>
    /// Minimal single-shot provider for <see cref="FakeModel"/> — returns a canned
    /// result with known usage/cost so the nested leaf calls a tool makes can be asserted.
    /// </summary>
    private sealed class FakeModelProvider : IModelProvider
    {
        public static readonly TokenUsage CallUsage = new()
        {
            InputTokens = 10,
            OutputTokens = 20,
            InputCost = new Price(0.01m),
            OutputCost = new Price(0.02m),
        };

        public string Name => "fake-provider";
        public bool SupportsModel(ILlm llm) => llm is FakeModel;

        public Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
            ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default)
        {
            var result = new Result<string>
            {
                Value = "sub:" + prompt.Text,
                MetaData = new MetaData
                {
                    Model = llm,
                    Provider = Name,
                    PromptName = "sub",
                    Usage = CallUsage,
                    Duration = TimeSpan.FromMilliseconds(7),
                    RequestId = "req-" + prompt.Text,
                },
            };
            return Task.FromResult((Result<TResponse>)(object)result);
        }

        public Task<Result<Asset>> GenerateImageAsync(IImageLlm llm, IPrompt<Asset> prompt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Result<Asset>> GenerateVideoAsync(IVideoLlm llm, IPrompt<Asset> prompt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Result<float[]>> EmbedAsync(IEmbeddingLlm llm, string input, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
            ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Result<string>> TranscribeAsync(IAudioLlm llm, Asset audioFile, string? language = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>Tool that makes <c>calls</c> single-shot sub-model calls via the injected provider.</summary>
    private sealed class NestingTool : ToolBase<NestingTool.Input, NestingTool.Output>
    {
        private readonly IAiProvider _ai;
        private readonly int _calls;
        public NestingTool(IAiProvider ai, int calls) { _ai = ai; _calls = calls; }

        public override string Name => "nest";
        public override string Description => "Calls a sub-model N times.";

        public override async Task<Output> ExecuteAsync(Input input, CancellationToken cancellationToken)
        {
            var outputs = new List<string>();
            for (var i = 0; i < _calls; i++)
            {
                var r = await _ai.GenerateAsync(new FakeModel(), $"sub{i}", cancellationToken);
                outputs.Add(r.Value);
            }
            return new Output { Combined = string.Join(",", outputs) };
        }

        public class Input { public string? Ignored { get; init; } }
        public class Output { public required string Combined { get; init; } }
    }

    /// <summary>Tool that runs a nested sub-agent via the injected provider.</summary>
    private sealed class NestingAgentTool : ToolBase<NestingAgentTool.Input, NestingAgentTool.Output>
    {
        private readonly IAiProvider _ai;
        public NestingAgentTool(IAiProvider ai) { _ai = ai; }

        public override string Name => "nest_agent";
        public override string Description => "Runs a sub-agent.";

        public override async Task<Output> ExecuteAsync(Input input, CancellationToken cancellationToken)
        {
            var r = await _ai.GenerateAsync(
                new FakeModel(), "sub",
                tools: Array.Empty<ITool>(), mcps: null, options: null,
                cancellationToken: cancellationToken);
            return new Output { Sub = r.Value };
        }

        public class Input { public string? Ignored { get; init; } }
        public class Output { public required string Sub { get; init; } }
    }

    #endregion

    private static AgentTurn FinalTurn(string text, TokenUsage? usage = null) => new()
    {
        ToolCalls = Array.Empty<PendingToolCall>(),
        FinalText = text,
        Usage = usage ?? new TokenUsage(),
        Duration = TimeSpan.FromMilliseconds(10),
    };

    private static AgentTurn ToolCallTurn(params (string name, string args, string id)[] calls) => new()
    {
        ToolCalls = calls.Select(c => new PendingToolCall
        {
            Id = c.id,
            Name = c.name,
            Arguments = JsonDocument.Parse(c.args).RootElement.Clone(),
        }).ToList(),
        FinalText = null,
        Usage = new TokenUsage(),
        Duration = TimeSpan.FromMilliseconds(5),
    };

    private static (IAiProvider provider, ScriptedAgentAdapter adapter) BuildProvider(
        Action<ServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAi();

        var adapter = new ScriptedAgentAdapter();
        services.AddSingleton<IAgentProviderAdapter>(adapter);

        configure?.Invoke(services);

        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IAiProvider>(), adapter);
    }

    [Fact]
    public async Task RunAsync_SingleTurnFinalText_ShouldReturnResult()
    {
        var (provider, adapter) = BuildProvider();
        adapter.Turns.Enqueue(FinalTurn("hello world"));

        var result = await provider.GenerateAsync(
            new FakeModel(),
            "ping",
            tools: null, mcps: null, options: null);

        result.Value.Should().Be("hello world");
        result.Iterations.Should().Be(1);
        result.ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WithToolCall_ShouldExecuteToolAndContinue()
    {
        var (provider, adapter) = BuildProvider();
        adapter.Turns.Enqueue(ToolCallTurn(("echo", """{"message":"hi"}""", "c1")));
        adapter.Turns.Enqueue(FinalTurn("done"));

        var result = await provider.GenerateAsync(
            new FakeModel(),
            "say hi",
            tools: new ITool[] { new EchoTool() });

        result.Value.Should().Be("done");
        result.Iterations.Should().Be(2);
        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].Name.Should().Be("echo");
        result.ToolCalls[0].IsError.Should().BeFalse();
        result.ToolCalls[0].Output!.Value.GetProperty("echo").GetString().Should().Be("hi");
    }

    [Fact]
    public async Task RunAsync_ParallelToolCalls_ShouldExecuteAllAndPreserveOrder()
    {
        var (provider, adapter) = BuildProvider();
        adapter.Turns.Enqueue(ToolCallTurn(
            ("echo", """{"message":"a"}""", "c1"),
            ("echo", """{"message":"b"}""", "c2"),
            ("echo", """{"message":"c"}""", "c3")));
        adapter.Turns.Enqueue(FinalTurn("ok"));

        var result = await provider.GenerateAsync(
            new FakeModel(),
            "multi",
            tools: new ITool[] { new EchoTool() });

        result.ToolCalls.Should().HaveCount(3);
        result.ToolCalls.Select(t => t.Input.GetProperty("message").GetString())
            .Should().BeEquivalentTo(new[] { "a", "b", "c" }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public async Task RunAsync_ToolThrows_DefaultsToReturnErrorToModel()
    {
        var (provider, adapter) = BuildProvider();
        adapter.Turns.Enqueue(ToolCallTurn(("explode", "{}", "c1")));
        adapter.Turns.Enqueue(FinalTurn("recovered"));

        var result = await provider.GenerateAsync(
            new FakeModel(),
            "go",
            tools: new ITool[] { new ThrowingTool() });

        result.Value.Should().Be("recovered");
        result.ToolCalls.Should().ContainSingle().Which.IsError.Should().BeTrue();
        result.ToolCalls[0].Error.Should().Be("planned failure");
    }

    [Fact]
    public async Task RunAsync_IterationLimitExceeded_ShouldThrow()
    {
        var (provider, adapter) = BuildProvider();
        // Infinite-tool-call script.
        for (var i = 0; i < 10; i++)
            adapter.Turns.Enqueue(ToolCallTurn(("echo", """{"message":"x"}""", $"c{i}")));

        var act = async () => await provider.GenerateAsync(
            new FakeModel(),
            "loop",
            tools: new ITool[] { new EchoTool() },
            options: new AgentOptions { MaxIterations = 3 });

        var ex = await act.Should().ThrowAsync<AgentIterationLimitException>();
        ex.Which.Limit.Should().Be(3);
        ex.Which.Partial!.Iterations.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_AllowedToolsFilter_ShouldHideFilteredTools()
    {
        var (provider, adapter) = BuildProvider();
        adapter.Turns.Enqueue(FinalTurn("filtered"));

        await provider.GenerateAsync(
            new FakeModel(),
            "q",
            tools: new ITool[] { new EchoTool(), new ThrowingTool() },
            options: new AgentOptions { AllowedTools = new[] { "echo" } });

        adapter.SessionsCreated.Should().ContainSingle()
            .Which.Tools.Select(t => t.Name).Should().BeEquivalentTo(new[] { "echo" });
    }

    [Fact]
    public async Task RunAsync_ExplicitToolsList_IsAuthoritative_AndShadowsDIDefaults()
    {
        // EchoTool is globally registered via AddAiTools<>(), ThrowingTool is
        // passed per-call. The caller-supplied list is authoritative — only
        // ThrowingTool should reach the model. EchoTool MUST NOT silently
        // tag along, even though DefaultTools defaults to true. This is the
        // regression test for the leak where any DI-registered tool was
        // exposed to every agent call that supplied its own list.
        var (provider, adapter) = BuildProvider(s =>
        {
            s.AddAiTools<EchoTool>();
        });
        adapter.Turns.Enqueue(FinalTurn("ok"));

        await provider.GenerateAsync(
            new FakeModel(),
            "q",
            tools: new ITool[] { new ThrowingTool() });

        adapter.SessionsCreated.Should().ContainSingle()
            .Which.Tools.Select(t => t.Name)
            .Should().BeEquivalentTo(new[] { "explode" });
    }

    [Fact]
    public async Task RunAsync_NullTools_FallsBackToDIDefaults()
    {
        // Passing tools: null is the "I have no opinion" signal — DI defaults
        // (anything explicitly registered via AddAiTools<>()) DO apply.
        var (provider, adapter) = BuildProvider(s =>
        {
            s.AddAiTools<EchoTool>();
        });
        adapter.Turns.Enqueue(FinalTurn("ok"));

        await provider.GenerateAsync(
            new FakeModel(),
            "q",
            tools: null);

        adapter.SessionsCreated.Should().ContainSingle()
            .Which.Tools.Select(t => t.Name)
            .Should().BeEquivalentTo(new[] { "echo" });
    }

    [Fact]
    public async Task RunAsync_EmptyToolsList_ShadowsDIDefaults()
    {
        // Passing tools: [] is the explicit "no tools, period" signal — it
        // MUST shadow DI defaults the same way tools: [t1] does.
        var (provider, adapter) = BuildProvider(s =>
        {
            s.AddAiTools<EchoTool>();
        });
        adapter.Turns.Enqueue(FinalTurn("ok"));

        await provider.GenerateAsync(
            new FakeModel(),
            "q",
            tools: Array.Empty<ITool>());

        adapter.SessionsCreated.Should().ContainSingle()
            .Which.Tools.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_DefaultToolsFalse_HidesDIDefaults_WhenCallerToolsNull()
    {
        // With tools: null AND DefaultTools = false the runner must NOT pull
        // DI defaults — the agent should see no tools at all.
        var (provider, adapter) = BuildProvider(s =>
        {
            s.AddAiTools<EchoTool>();
        });
        adapter.Turns.Enqueue(FinalTurn("ok"));

        await provider.GenerateAsync(
            new FakeModel(),
            "q",
            tools: null,
            options: new AgentOptions { DefaultTools = false });

        adapter.SessionsCreated.Should().ContainSingle()
            .Which.Tools.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_AutoDiscoveredToolBase_IsNotExposedWithoutExplicitRegistration()
    {
        // EchoTool / ThrowingTool inherit from ToolBase<,> and are therefore
        // discovered by the source generator and registered as concrete
        // Scoped services. They MUST NOT be auto-enrolled into IToolRegistry
        // (the global "active for every call" channel) — only explicit
        // AddAiTools<>() puts a tool there. With no caller-supplied list and
        // no AddAiTools<>() registration, the agent must see zero tools.
        var (provider, adapter) = BuildProvider();
        adapter.Turns.Enqueue(FinalTurn("ok"));

        await provider.GenerateAsync(
            new FakeModel(),
            "q",
            tools: null);

        adapter.SessionsCreated.Should().ContainSingle()
            .Which.Tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateStreamAsync_ShouldEmitOrderedEventsEndingWithCompleted()
    {
        var (provider, adapter) = BuildProvider();
        adapter.Turns.Enqueue(ToolCallTurn(("echo", """{"message":"hi"}""", "c1")));
        adapter.Turns.Enqueue(FinalTurn("stream done"));

        var events = new List<AgentEvent>();
        await foreach (var evt in provider.GenerateStreamAsync(
            new FakeModel(),
            new TestPrompt(),
            tools: new ITool[] { new EchoTool() }))
        {
            events.Add(evt);
        }

        events.Should().NotBeEmpty();
        events.OfType<AgentIterationStartedEvent>().Should().HaveCount(2);
        events.OfType<AgentTurnCompletedEvent>().Should().HaveCount(2);
        events.OfType<AgentToolCallStartedEvent>().Should().ContainSingle();
        events.OfType<AgentToolCallCompletedEvent>().Should().ContainSingle();
        events.Last().Should().BeOfType<AgentCompletedEvent<string>>();

        var completed = (AgentCompletedEvent<string>)events.Last();
        completed.Result.Value.Should().Be("stream done");
    }

    [Fact]
    public async Task GenerateStreamAsync_OnIterationLimit_ShouldEmitFailedEvent()
    {
        var (provider, adapter) = BuildProvider();
        for (var i = 0; i < 5; i++)
            adapter.Turns.Enqueue(ToolCallTurn(("echo", """{"message":"x"}""", $"c{i}")));

        var events = new List<AgentEvent>();
        await foreach (var evt in provider.GenerateStreamAsync(
            new FakeModel(),
            new TestPrompt(),
            tools: new ITool[] { new EchoTool() },
            options: new AgentOptions { MaxIterations = 2 }))
        {
            events.Add(evt);
        }

        events.Last().Should().BeOfType<AgentFailedEvent>();
        var failed = (AgentFailedEvent)events.Last();
        failed.Error.Should().BeOfType<AgentIterationLimitException>();
        failed.Partial!.Iterations.Should().Be(2);
    }

    private sealed class TestPrompt : IPrompt<string>
    {
        public string Text => "hello";
        public IReadOnlyList<Asset>? Files => null;
    }

    #region Nested usage tracking

    [Fact]
    public async Task PlainRun_PopulatesUsageTree_WithNoNestedCalls()
    {
        var (provider, adapter) = BuildProvider();
        adapter.Turns.Enqueue(FinalTurn("hi", new TokenUsage { InputTokens = 3, OutputTokens = 4 }));

        var result = await provider.GenerateAsync(new FakeModel(), "ping");

        result.Usage.Should().NotBeNull();
        result.Usage!.Kind.Should().Be(AiUsageKind.Agent);
        result.Usage.Model.Should().Be("fake-agent");
        result.Usage.Children.Should().BeEmpty();
        result.Usage.Usage.InputTokens.Should().Be(3);
        result.NestedAiCalls.Should().BeEmpty();

        // No nesting → Request == Total.
        result.Request.Tokens.InputTokens.Should().Be(3);
        result.Request.Calls.Should().Be(1);
        result.Total.Tokens.InputTokens.Should().Be(3);
        result.Total.Calls.Should().Be(1);
    }

    [Fact]
    public async Task NestedLeafCalls_AreAttachedToTheRunTree()
    {
        // A tool that injects IAiProvider and calls a sub-model 3x — exactly the
        // "tool calls ChatAsync 5 times and we can't see it" scenario.
        var (provider, adapter) = BuildProvider(s => s.AddSingleton<IModelProvider, FakeModelProvider>());
        adapter.Turns.Enqueue(ToolCallTurn(("nest", "{}", "c1")));
        adapter.Turns.Enqueue(FinalTurn("done"));

        var tool = new NestingTool(provider, calls: 3);
        var result = await provider.GenerateAsync(new FakeModel(), "go", tools: new ITool[] { tool });

        result.Value.Should().Be("done");

        // Tree: Agent -> Tool("nest") -> 3x Generate.
        result.Usage.Should().NotBeNull();
        result.Usage!.Kind.Should().Be(AiUsageKind.Agent);
        var toolNode = result.Usage.Children.Should().ContainSingle().Subject;
        toolNode.Kind.Should().Be(AiUsageKind.Tool);
        toolNode.ToolName.Should().Be("nest");
        toolNode.Children.Should().HaveCount(3).And.OnlyContain(c => c.Kind == AiUsageKind.Generate);

        // Flat list of sub-model calls — like ToolCalls, but for AI — with full detail.
        result.NestedAiCalls.Should().HaveCount(3);
        result.NestedAiCalls.Should().OnlyContain(c => c.Model == "fake-agent");
        result.NestedAiCalls.Should().OnlyContain(c => c.ToolName == "nest"); // knows its origin
        result.NestedAiCalls[0].Output.Should().Be("sub:sub0");   // output captured
        result.NestedAiCalls[0].Input.Should().Be("sub0");        // input captured
        result.NestedAiCalls[0].Usage.InputTokens.Should().Be(10);
        result.NestedAiCalls[0].RequestId.Should().Be("req-sub0");

        // Per-tool nested rollup surfaced on the ToolInvocation.
        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].NestedCalls.Should().Be(3);
        result.ToolCalls[0].NestedUsage!.InputTokens.Should().Be(30);
        result.ToolCalls[0].NestedCost!.Value.Value.Should().Be(0.09m);

        // Total = whole tree (incl nested). The agent's own turns were free, so the
        // whole cost is the 3 sub-calls. Request = main agent only → free.
        result.Total.Cost.Value.Should().Be(0.09m);
        result.Total.Tokens.InputTokens.Should().Be(30);
        result.Total.Calls.Should().Be(5);   // 2 outer turns + 3 nested calls
        result.Request.Cost.Value.Should().Be(0m);
        result.Request.Calls.Should().Be(2); // tool-call turn + final turn
    }

    [Fact]
    public async Task NestedAgentCall_AppearsAsAgentNodeInTree()
    {
        var (provider, adapter) = BuildProvider();
        var subUsage = new TokenUsage
        {
            InputTokens = 5,
            OutputTokens = 7,
            InputCost = new Price(0.05m),
            OutputCost = new Price(0.07m),
        };
        adapter.Turns.Enqueue(ToolCallTurn(("nest_agent", "{}", "c1"))); // outer turn 1
        adapter.Turns.Enqueue(FinalTurn("subdone", subUsage));           // nested agent's only turn
        adapter.Turns.Enqueue(FinalTurn("done"));                        // outer final

        var tool = new NestingAgentTool(provider);
        var result = await provider.GenerateAsync(new FakeModel(), "go", tools: new ITool[] { tool });

        result.Value.Should().Be("done");

        // Tree: Agent -> Tool -> Agent (the sub-agent).
        var toolNode = result.Usage!.Children.Should().ContainSingle().Subject;
        toolNode.Kind.Should().Be(AiUsageKind.Tool);
        var subAgent = toolNode.Children.Should().ContainSingle().Subject;
        subAgent.Kind.Should().Be(AiUsageKind.Agent);
        subAgent.Model.Should().Be("fake-agent");
        subAgent.ToolName.Should().Be("nest_agent"); // sub-agent knows which tool spawned it
        subAgent.Usage.InputTokens.Should().Be(5);

        result.NestedAiCalls.Should().ContainSingle().Which.Kind.Should().Be(AiUsageKind.Agent);
        result.ToolCalls[0].NestedCalls.Should().Be(1);
        result.Total.Cost.Value.Should().Be(0.12m); // 0.05 + 0.07
        result.Total.Calls.Should().Be(3);          // 2 outer turns + 1 sub-agent turn
    }

    [Fact]
    public async Task MaxNestedDepth_ThrowsWhenSubAgentTooDeep()
    {
        // Only the root agent is allowed (depth 1); a sub-agent started by a tool is
        // depth 2 and must be rejected. ThrowToCaller surfaces it instead of folding
        // it into a tool error.
        var (provider, adapter) = BuildProvider(s => s.Configure<AiOptions>(o =>
        {
            o.Agent.MaxNestedDepth = 1;
            o.Agent.OnToolException = ToolExceptionPolicy.ThrowToCaller;
        }));
        adapter.Turns.Enqueue(ToolCallTurn(("nest_agent", "{}", "c1")));

        var tool = new NestingAgentTool(provider);
        var act = async () => await provider.GenerateAsync(new FakeModel(), "go", tools: new ITool[] { tool });

        await act.Should().ThrowAsync<AiNestingLimitException>();
    }

    #endregion
}

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
    public async Task RunAsync_DefaultTools_AreAddedToCallerTools()
    {
        // EchoTool registered as default; per-call also passes a fresh ThrowingTool.
        // Both should be visible to the model (additive).
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
            .Should().BeEquivalentTo(new[] { "explode", "echo" });
    }

    [Fact]
    public async Task RunAsync_DefaultToolsFalse_HidesGloballyRegisteredTools()
    {
        var (provider, adapter) = BuildProvider(s =>
        {
            s.AddAiTools<EchoTool>();
        });
        adapter.Turns.Enqueue(FinalTurn("ok"));

        await provider.GenerateAsync(
            new FakeModel(),
            "q",
            tools: new ITool[] { new ThrowingTool() },
            options: new AgentOptions { DefaultTools = false });

        adapter.SessionsCreated.Should().ContainSingle()
            .Which.Tools.Select(t => t.Name)
            .Should().BeEquivalentTo(new[] { "explode" });
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
        public string? System => null;
        public IReadOnlyList<Asset>? Files => null;
    }
}

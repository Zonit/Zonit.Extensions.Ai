using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Tests for the CLI-backed agent session — argv construction (mcp-config, allowed
/// tools, model), result parsing, and the no-bridge guard. Uses a fake CLI runner and a
/// fake tool bridge; no real <c>claude</c> process or loopback server is involved.
/// </summary>
public class CliAgentSessionTests
{
    [Fact]
    public async Task RunTurnAsync_WithTools_BuildsArgvAndReturnsFinalTurn()
    {
        var runner = new FakeCliRunner
        {
            Json = """{"type":"result","result":"FINAL ANSWER","session_id":"sx","usage":{"input_tokens":7,"output_tokens":3}}""",
        };
        var session = NewSession(CliTestSupport.Context([new EchoTool()]), runner, new FakeBridge());

        var turn = await session.RunTurnAsync(null, CancellationToken.None);

        turn.FinalText.Should().Be("FINAL ANSWER");
        turn.ToolCalls.Should().BeEmpty();
        turn.Usage!.OutputTokens.Should().Be(3);
        turn.Usage.InputTokens.Should().Be(7);

        var args = runner.Last!.Arguments;
        args.Should().Contain("--model");
        args.Should().Contain("claude-sonnet-4-6");
        args.Should().Contain("--output-format");
        args.Should().Contain("json");
        args.Should().Contain("--mcp-config");
        args.Should().Contain("mcp__zonit__echo");
        args.Should().Contain(a => a.Contains("127.0.0.1") && a.Contains("Bearer test-token"));
    }

    [Fact]
    public async Task RunTurnAsync_NoTools_OmitsMcpConfig()
    {
        var runner = new FakeCliRunner();
        var session = NewSession(CliTestSupport.Context([]), runner, new FakeBridge());

        await session.RunTurnAsync(null, CancellationToken.None);

        runner.Last!.Arguments.Should().NotContain("--mcp-config");
    }

    [Fact]
    public async Task RunTurnAsync_ToolsButNoBridge_Throws()
    {
        var session = NewSession(CliTestSupport.Context([new EchoTool()]), new FakeCliRunner(), bridge: null);

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task RunTurnAsync_NonZeroExit_Throws()
    {
        var runner = new FakeCliRunner { ExitCode = 1, Stderr = "kaboom" };
        var session = NewSession(CliTestSupport.Context([]), runner, new FakeBridge());

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("kaboom");
    }

    private static CliAgentSession NewSession(AgentSessionContext ctx, IClaudeCliRunner runner, IAgentToolBridge? bridge)
    {
        var options = new AnthropicOptions { Transport = AnthropicTransport.Sdk };
        options.Cli.ExecutablePath = typeof(CliAgentSessionTests).Assembly.Location; // exists → locator succeeds
        return new CliAgentSession(ctx, runner, bridge, options, new AiResilienceOptions(), NullLogger.Instance);
    }
}

/// <summary>Routing matrix for <see cref="AnthropicAgentAdapter"/> across transport modes.</summary>
public class AnthropicAgentAdapterRoutingTests
{
    private const string MissingExe = "/zonit/definitely/not/here/claude";

    [Fact]
    public void Api_ReturnsHttpSession()
    {
        var session = Adapter(new AnthropicOptions { Transport = AnthropicTransport.Api, ApiKey = "k" })
            .BeginSession(CliTestSupport.Context([]));
        session.Should().BeOfType<AnthropicAgentSession>();
    }

    [Fact]
    public void Sdk_NoTools_ReturnsCliSession()
    {
        var options = new AnthropicOptions { Transport = AnthropicTransport.Sdk };
        options.Cli.ExecutablePath = ValidExe;
        Adapter(options).BeginSession(CliTestSupport.Context([])).Should().BeOfType<CliAgentSession>();
    }

    [Fact]
    public void Sdk_WithToolsAndBridge_ReturnsCliSession()
    {
        var options = new AnthropicOptions { Transport = AnthropicTransport.Sdk };
        options.Cli.ExecutablePath = ValidExe;
        Adapter(options, bridge: new FakeBridge())
            .BeginSession(CliTestSupport.Context([new EchoTool()]))
            .Should().BeOfType<CliAgentSession>();
    }

    [Fact]
    public void Sdk_WithToolsNoBridge_Throws()
    {
        var options = new AnthropicOptions { Transport = AnthropicTransport.Sdk };
        options.Cli.ExecutablePath = ValidExe;
        var act = () => Adapter(options).BeginSession(CliTestSupport.Context([new EchoTool()]));
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Auto_CliMissing_WithApiKey_FallsBackToHttp()
    {
        var options = new AnthropicOptions { Transport = AnthropicTransport.Auto, ApiKey = "k" };
        options.Cli.ExecutablePath = MissingExe;
        Adapter(options).BeginSession(CliTestSupport.Context([])).Should().BeOfType<AnthropicAgentSession>();
    }

    [Fact]
    public void Auto_CliMissing_NoApiKey_Throws()
    {
        var options = new AnthropicOptions { Transport = AnthropicTransport.Auto };
        options.Cli.ExecutablePath = MissingExe;
        var act = () => Adapter(options).BeginSession(CliTestSupport.Context([]));
        act.Should().Throw<NotSupportedException>();
    }

    private static string ValidExe => typeof(AnthropicAgentAdapterRoutingTests).Assembly.Location;

    private static AnthropicAgentAdapter Adapter(AnthropicOptions options, IAgentToolBridge? bridge = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClaudeCliRunner>(new FakeCliRunner());
        if (bridge is not null) services.AddSingleton(bridge);
        var sp = services.BuildServiceProvider();

        return new AnthropicAgentAdapter(
            new HttpClient(),
            sp,
            Options.Create(options),
            Options.Create(new AiOptions()),
            NullLogger<AnthropicAgentAdapter>.Instance);
    }
}

// ---- shared test helpers --------------------------------------------------------

internal static class CliTestSupport
{
    public static AgentSessionContext Context(IReadOnlyList<ITool> tools) => new()
    {
        Llm = new Sonnet46(),
        Prompt = new StubPrompt { Text = "Do the thing." },
        ResponseType = null,
        Tools = tools,
    };
}

internal sealed class StubPrompt : IPrompt
{
    public required string Text { get; init; }
    public IReadOnlyList<Asset>? Files => null;
}

internal sealed class EchoTool : ITool
{
    public string Name => "echo";
    public string Description => "Echoes the input value.";
    public JsonElement InputSchema =>
        JsonDocument.Parse("""{"type":"object","properties":{"value":{"type":"string"}}}""").RootElement;

    public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var value = arguments.TryGetProperty("value", out var v) ? v.GetString() : "(none)";
        return Task.FromResult(JsonSerializer.SerializeToElement("echo:" + value));
    }
}

internal sealed class FakeCliRunner : IClaudeCliRunner
{
    public ClaudeCliInvocation? Last;
    public string Json = """{"type":"result","result":"ok","session_id":"s","usage":{"input_tokens":1,"output_tokens":1}}""";
    public int ExitCode;
    public string Stderr = "";

    public Task<ClaudeProcessResult> RunAsync(ClaudeCliInvocation invocation, CancellationToken cancellationToken)
    {
        Last = invocation;
        return Task.FromResult(new ClaudeProcessResult
        {
            ExitCode = ExitCode,
            StandardOutput = Json,
            StandardError = Stderr,
        });
    }

    public async IAsyncEnumerable<string> StreamLinesAsync(
        ClaudeCliInvocation invocation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Last = invocation;
        await Task.CompletedTask;
        yield break;
    }
}

internal sealed class FakeBridge : IAgentToolBridge
{
    public Task<IAgentToolBridgeSession> StartAsync(IReadOnlyList<ITool> tools, CancellationToken cancellationToken)
    {
        var names = tools.Select(t => t.Name).ToArray();
        return Task.FromResult<IAgentToolBridgeSession>(new FakeSession(names));
    }

    private sealed class FakeSession : IAgentToolBridgeSession
    {
        public FakeSession(IReadOnlyList<string> toolNames) => ToolNames = toolNames;
        public string ServerName => "zonit";
        public Uri Url => new("http://127.0.0.1:65000/mcp");
        public string? AuthToken => "test-token";
        public IReadOnlyList<string> ToolNames { get; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

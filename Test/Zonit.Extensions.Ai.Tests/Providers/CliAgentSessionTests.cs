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
/// Tests for the CLI-backed agent session — argv construction (mcp-config, allowed tools, model,
/// isolation, --max-turns), the prompt on stdin, stream-json parsing of tool calls and iterations,
/// and the no-bridge guard. Uses a fake CLI runner and a fake tool bridge; no real <c>claude</c>
/// process or loopback server is involved.
/// </summary>
public class CliAgentSessionTests
{
    [Fact]
    public async Task RunTurnAsync_WithTools_BuildsArgvAndReturnsFinalTurn()
    {
        var runner = new FakeCliRunner
        {
            Json = """{"type":"result","subtype":"success","result":"FINAL ANSWER","session_id":"sx","num_turns":1,"usage":{"input_tokens":7,"output_tokens":3}}""",
        };
        var session = NewSession(CliTestSupport.Context([new EchoTool()]), runner, new FakeBridge());

        var turn = await session.RunTurnAsync(null, CancellationToken.None);

        turn.FinalText.Should().Be("FINAL ANSWER");
        turn.ToolCalls.Should().BeEmpty();
        turn.Usage!.OutputTokens.Should().Be(3);
        turn.Usage.InputTokens.Should().Be(7);

        var args = runner.Last!.Arguments.ToList();
        args.Should().Contain("--model");
        args.Should().Contain("claude-sonnet-4-6");
        args.Should().ContainInOrder("--output-format", "stream-json");
        args.Should().Contain("--verbose");
        args.Should().Contain("--mcp-config");
        args.Should().Contain("mcp__zonit__echo");

        // The prompt travels on stdin, never on the command line.
        runner.Last.StandardInput.Should().Be("Do the thing.");
        args.Should().NotContain("Do the thing.");

        // The MCP config must be passed as a temp FILE PATH, not inline JSON: on Windows an
        // npm `claude.cmd` is launched via cmd.exe, which mangles an inline {"mcpServers":…}
        // arg and leaves the CLI with no tools. The fake runner read the file mid-run.
        var mcpPath = args[args.IndexOf("--mcp-config") + 1];
        mcpPath.Should().EndWith(".json");
        mcpPath.Should().NotContain("{");
        runner.McpConfigContent.Should().NotBeNull();
        runner.McpConfigContent!.Should().Contain("127.0.0.1").And.Contain("Bearer test-token");
        // The temp files are cleaned up once the run completes.
        System.IO.File.Exists(mcpPath).Should().BeFalse();
    }

    [Fact]
    public async Task RunTurnAsync_Isolated_ByDefault_SeesOnlyTheBridge()
    {
        var runner = new FakeCliRunner();
        var session = NewSession(CliTestSupport.Context([new EchoTool()]), runner, new FakeBridge());

        await session.RunTurnAsync(null, CancellationToken.None);

        var args = runner.Last!.Arguments.ToList();
        args.Should().ContainInOrder("--tools", "");
        args.Should().Contain("--strict-mcp-config");
        args.Should().ContainInOrder("--setting-sources", "local");
        args.Should().Contain("--disable-slash-commands");
        args.Should().Contain("--no-session-persistence");

        // Claude Code's system prompt is replaced; a standalone run has no system text, as on the API.
        args.Should().Contain("--system-prompt-file").And.NotContain("--append-system-prompt-file");
        runner.SystemPromptContent.Should().BeEmpty();

        // A fresh, empty working directory that is removed afterwards.
        runner.WorkingDirectoryWasEmpty.Should().BeTrue();
        System.IO.Directory.Exists(runner.Last.WorkingDirectory).Should().BeFalse();
    }

    [Fact]
    public async Task RunTurnAsync_NotIsolated_RunsTheFullClaudeCodeSession()
    {
        var runner = new FakeCliRunner();
        var session = NewSession(CliTestSupport.Context([]), runner, new FakeBridge(), o => o.Cli.Isolated = false);

        await session.RunTurnAsync(null, CancellationToken.None);

        var args = runner.Last!.Arguments.ToList();
        args.Should().NotContain("--tools").And.NotContain("--strict-mcp-config").And.NotContain("--setting-sources");
        args.Should().NotContain("--system-prompt-file").And.NotContain("--append-system-prompt-file");
        runner.Last.WorkingDirectory.Should().BeNull("the host process directory is used");
    }

    [Fact]
    public async Task RunTurnAsync_SeededChat_SendsPromptTextAsTheSystemPrompt()
    {
        var runner = new FakeCliRunner();
        var context = CliTestSupport.Context([], chat: [new User("What is 2+2?")], prompt: "You are a calculator.");
        var session = NewSession(context, runner, new FakeBridge());

        await session.RunTurnAsync(null, CancellationToken.None);

        runner.SystemPromptContent.Should().Be("You are a calculator.");
        runner.Last!.StandardInput.Should().Be("User: What is 2+2?");
    }

    [Fact]
    public async Task RunTurnAsync_LongPrompt_IsNotLimitedByTheCommandLine()
    {
        // 42,000 characters: above cmd.exe's 8,191 and CreateProcess's 32,767.
        var prompt = string.Join(" ", Enumerable.Repeat("filler", 6000));
        var runner = new FakeCliRunner();
        var session = NewSession(CliTestSupport.Context([], prompt: prompt), runner, new FakeBridge());

        await session.RunTurnAsync(null, CancellationToken.None);

        runner.Last!.StandardInput.Should().Be(prompt);
        runner.Last.Arguments.Sum(a => a.Length).Should().BeLessThan(2_000);
    }

    [Fact]
    public async Task RunTurnAsync_PassesMaxIterationsAsMaxTurns()
    {
        var runner = new FakeCliRunner();
        var session = NewSession(CliTestSupport.Context([], maxIterations: 40), runner, new FakeBridge());

        await session.RunTurnAsync(null, CancellationToken.None);

        runner.Last!.Arguments.Should().ContainInOrder("--max-turns", "40");
    }

    [Fact]
    public async Task RunTurnAsync_ReportsToolCallsAndIterations_FromTheStream()
    {
        var runner = new FakeCliRunner { Lines = CliTestSupport.ThreeCallRun };
        var session = NewSession(CliTestSupport.Context([new EchoTool()]), runner, new FakeBridge());

        var turn = await session.RunTurnAsync(null, CancellationToken.None);

        turn.FinalText.Should().Be("done");
        turn.ToolCalls.Should().BeEmpty("the CLI already ran them");
        turn.Iterations.Should().Be(2, "two model responses: one with two tool calls, one with the answer");

        var calls = turn.CompletedToolCalls!;
        calls.Select(c => c.Name).Should().Equal("echo", "echo", "Read");
        calls.Select(c => c.Iteration).Should().Equal(1, 1, 2);
        calls[0].McpServer.Should().BeNull("bridge tools are the caller's own tools");
        calls[0].Input.GetProperty("value").GetString().Should().Be("a");
        calls[0].Output!.Value.GetString().Should().Be("echo:a");
        calls[1].Output!.Value.GetProperty("n").GetInt32().Should().Be(2, "a JSON result is parsed back");
        calls[2].IsError.Should().BeTrue();
        calls[2].Error.Should().Be("File does not exist.");
        calls[2].Output.Should().BeNull();
    }

    [Fact]
    public async Task RunTurnAsync_MaxTurnsReached_ThrowsIterationLimit_WithWhatRan()
    {
        var runner = new FakeCliRunner
        {
            ExitCode = 1, // the CLI exits non-zero after reporting error_max_turns
            Lines =
            [
                """{"type":"assistant","message":{"id":"msg_1","content":[{"type":"tool_use","id":"t1","name":"mcp__zonit__echo","input":{"value":"a"}}]}}""",
                """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"echo:a"}]}}""",
                """{"type":"result","subtype":"error_max_turns","is_error":true,"num_turns":2,"session_id":"s","usage":{"input_tokens":5,"output_tokens":2}}""",
            ],
        };
        var session = NewSession(CliTestSupport.Context([new EchoTool()], maxIterations: 1), runner, new FakeBridge());

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<AgentIterationLimitException>()).Which;
        ex.Limit.Should().Be(1);
        ex.Partial!.Iterations.Should().Be(1);
        ex.Partial.ToolCalls.Should().ContainSingle().Which.Name.Should().Be("echo");
        ex.Partial.TotalUsage.OutputTokens.Should().Be(2);
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
        var runner = new FakeCliRunner { ExitCode = 1, Stderr = "kaboom", Lines = [] };
        var session = NewSession(CliTestSupport.Context([]), runner, new FakeBridge());

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("kaboom");
    }

    [Fact]
    public async Task RunTurnAsync_ErrorResult_Throws()
    {
        var runner = new FakeCliRunner
        {
            Lines = ["""{"type":"result","subtype":"error_during_execution","is_error":true,"result":"API overloaded"}"""],
        };
        var session = NewSession(CliTestSupport.Context([]), runner, new FakeBridge());

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("API overloaded");
    }

    private static CliAgentSession NewSession(
        AgentSessionContext ctx, IClaudeCliRunner runner, IAgentToolBridge? bridge, Action<AnthropicOptions>? configure = null)
    {
        var options = new AnthropicOptions { Transport = AnthropicTransport.Sdk };
        options.Cli.ExecutablePath = typeof(CliAgentSessionTests).Assembly.Location; // exists → locator succeeds
        configure?.Invoke(options);
        return new CliAgentSession(ctx, runner, bridge, options, new AiResilienceOptions(), NullLogger.Instance);
    }
}

/// <summary>Parsing of the CLI's stream-json agent output, line by line.</summary>
public class ClaudeCliRunLogTests
{
    [Fact]
    public void SubAgentLines_AreNotCounted()
    {
        var log = new ClaudeCliRunLog("zonit");
        log.Accept("""{"type":"assistant","message":{"id":"m1","content":[{"type":"tool_use","id":"t1","name":"Agent","input":{}}]}}""", TimeSpan.Zero);
        log.Accept("""{"type":"assistant","parent_tool_use_id":"t1","message":{"id":"sub","content":[{"type":"tool_use","id":"t2","name":"Read","input":{}}]}}""", TimeSpan.Zero);
        log.Accept("""{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"sub done"}]}}""", TimeSpan.Zero);

        log.Iterations.Should().Be(1);
        log.ToolCalls().Should().ContainSingle().Which.Name.Should().Be("Agent");
    }

    [Fact]
    public void ToolCallWithoutResult_IsReportedAsFailed()
    {
        var log = new ClaudeCliRunLog("zonit");
        log.Accept("""{"type":"assistant","message":{"id":"m1","content":[{"type":"tool_use","id":"t1","name":"mcp__zonit__echo","input":{}}]}}""", TimeSpan.Zero);

        var call = log.ToolCalls().Single();
        call.IsError.Should().BeTrue();
        call.Error.Should().Contain("no result");
    }

    [Fact]
    public void ResultContentBlocks_AreJoined_AndDurationIsMeasured()
    {
        var log = new ClaudeCliRunLog("zonit");
        log.Accept("""{"type":"assistant","message":{"id":"m1","content":[{"type":"tool_use","id":"t1","name":"mcp__zonit__echo","input":{}}]}}""", TimeSpan.FromMilliseconds(100));
        log.Accept("""{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":[{"type":"text","text":"line 1"},{"type":"text","text":"line 2"}]}]}}""", TimeSpan.FromMilliseconds(350));

        var call = log.ToolCalls().Single();
        call.Output!.Value.GetString().Should().Be("line 1\nline 2");
        call.Duration.Should().Be(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void NonJsonLines_AreIgnored()
    {
        var log = new ClaudeCliRunLog(null);
        log.Accept("warning: something on stdout", TimeSpan.Zero);
        log.Accept("""{"type":"result","subtype":"success","result":"ok"}""", TimeSpan.Zero);

        log.Result!.Result.Should().Be("ok");
        log.Iterations.Should().Be(0);
    }

    [Theory]
    [InlineData("mcp__zonit__echo", "zonit", "echo", null)]
    [InlineData("mcp__github__read_file", "zonit", "read_file", "github")]
    [InlineData("mcp__claude_ai_Docs__batch", "zonit", "batch", "claude_ai_Docs")]
    [InlineData("Read", "zonit", "Read", null)]
    [InlineData("mcp__broken", "zonit", "mcp__broken", null)]
    public void SplitToolName_SeparatesTheMcpServer(string raw, string bridge, string name, string? server)
        => ClaudeCliRunLog.SplitToolName(raw, bridge).Should().Be((name, server));
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
    public static AgentSessionContext Context(
        IReadOnlyList<ITool> tools,
        int? maxIterations = null,
        IReadOnlyList<ChatMessage>? chat = null,
        string prompt = "Do the thing.") => new()
    {
        Llm = new Sonnet46(),
        Prompt = new StubPrompt { Text = prompt },
        ResponseType = null,
        Tools = tools,
        InitialChat = chat,
        MaxIterations = maxIterations,
    };

    /// <summary>
    /// A run shaped like the real CLI output: one response with two bridge tool calls (each
    /// result arriving before the next block), then a response that calls a built-in tool,
    /// which fails, and answers.
    /// </summary>
    public static readonly string[] ThreeCallRun =
    [
        """{"type":"system","subtype":"init"}""",
        """{"type":"assistant","message":{"id":"msg_A","content":[{"type":"thinking","thinking":""}]}}""",
        """{"type":"assistant","message":{"id":"msg_A","content":[{"type":"tool_use","id":"t1","name":"mcp__zonit__echo","input":{"value":"a"}}]}}""",
        """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"echo:a"}]}}""",
        """{"type":"assistant","message":{"id":"msg_A","content":[{"type":"tool_use","id":"t2","name":"mcp__zonit__echo","input":{"value":"b"}}]}}""",
        """{"type":"rate_limit_event"}""",
        """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t2","content":[{"type":"text","text":"{\"n\":2}"}]}]}}""",
        """{"type":"assistant","message":{"id":"msg_B","content":[{"type":"tool_use","id":"t3","name":"Read","input":{"file_path":"x"}}]}}""",
        """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t3","is_error":true,"content":"File does not exist."}]}}""",
        """{"type":"assistant","message":{"id":"msg_B","content":[{"type":"text","text":"done"}]}}""",
        """{"type":"result","subtype":"success","is_error":false,"result":"done","num_turns":3,"session_id":"s1","usage":{"input_tokens":4,"output_tokens":9}}""",
    ];
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
    /// <summary>The terminal result line; the whole output when <see cref="Lines"/> is unset.</summary>
    public string Json = """{"type":"result","subtype":"success","result":"ok","session_id":"s","usage":{"input_tokens":1,"output_tokens":1}}""";
    /// <summary>stream-json output lines; <c>null</c> = just <see cref="Json"/>.</summary>
    public string[]? Lines;
    public int ExitCode;
    public string Stderr = "";
    /// <summary>Content of the --mcp-config file, captured mid-run (the real CLI reads it before exit).</summary>
    public string? McpConfigContent;
    /// <summary>Content of the --system-prompt-file / --append-system-prompt-file file, captured mid-run.</summary>
    public string? SystemPromptContent;
    /// <summary>Whether the invocation's working directory existed and was empty while the run was live.</summary>
    public bool WorkingDirectoryWasEmpty;

    public Task<ClaudeProcessResult> RunAsync(ClaudeCliInvocation invocation, CancellationToken cancellationToken)
    {
        Capture(invocation);
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
        Capture(invocation);
        foreach (var line in Lines ?? [Json])
        {
            await Task.Yield();
            yield return line;
        }

        // Like ClaudeCliProcess: a non-zero exit throws once the output has been read.
        if (ExitCode != 0)
            throw new InvalidOperationException($"Claude CLI exited with code {ExitCode}. {Stderr}");
    }

    // Mimics the real CLI reading its files while the process runs (before the session deletes
    // them), so tests can assert on their content.
    private void Capture(ClaudeCliInvocation invocation)
    {
        Last = invocation;
        McpConfigContent = ReadFileArgument(invocation, "--mcp-config");
        SystemPromptContent = ReadFileArgument(invocation, "--system-prompt-file")
            ?? ReadFileArgument(invocation, "--append-system-prompt-file");
        WorkingDirectoryWasEmpty = invocation.WorkingDirectory is { } dir
            && System.IO.Directory.Exists(dir)
            && !System.IO.Directory.EnumerateFileSystemEntries(dir).Any();
    }

    private static string? ReadFileArgument(ClaudeCliInvocation invocation, string flag)
    {
        var args = invocation.Arguments.ToList();
        var idx = args.IndexOf(flag);
        if (idx < 0 || idx + 1 >= args.Count) return null;
        var path = args[idx + 1];
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
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

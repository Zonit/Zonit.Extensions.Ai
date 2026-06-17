using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Unit tests for the Claude Code CLI transport (<c>claude -p</c>) — driven through a
/// fake <see cref="IClaudeCliRunner"/> so no real <c>claude</c> binary or process is
/// needed at build/test time.
/// </summary>
public class AnthropicCliTransportTests
{
    [Fact]
    public async Task SendAsync_PlainText_BuildsArgvAndSynthesizesResponse()
    {
        var runner = new FakeRunner
        {
            JsonStdout = """{"type":"result","result":"Hello there","session_id":"sess_1","usage":{"input_tokens":11,"output_tokens":4,"cache_read_input_tokens":2,"cache_creation_input_tokens":1}}""",
        };
        var transport = CreateTransport(runner, new AnthropicOptions { Transport = AnthropicTransport.Sdk });

        var response = await transport.SendAsync(new Sonnet46(), TextRequest("Say hi"), "GenerateAsync", CancellationToken.None);

        response.Content.Should().ContainSingle();
        response.Content![0].Type.Should().Be("text");
        response.Content[0].Text.Should().Be("Hello there");
        response.Id.Should().Be("sess_1");
        response.StopReason.Should().Be("end_turn");
        response.Usage!.InputTokens.Should().Be(11);
        response.Usage.OutputTokens.Should().Be(4);
        response.Usage.CacheReadInputTokens.Should().Be(2);
        response.Usage.CacheCreationInputTokens.Should().Be(1);

        var args = runner.LastInvocation!.Arguments;
        args.Should().Contain("--print");
        args.Should().Contain("--output-format");
        args.Should().Contain("json");
        args.Should().Contain("--model");
        args.Should().Contain("claude-sonnet-4-6");
    }

    [Fact]
    public async Task SendAsync_StructuredOutput_InjectsSchemaIntoSystemPrompt_NotAsTool()
    {
        var runner = new FakeRunner
        {
            JsonStdout = """{"type":"result","result":"{\"name\":\"Ada\"}","session_id":"s"}""",
        };
        var transport = CreateTransport(runner, new AnthropicOptions { Transport = AnthropicTransport.Sdk });

        var request = TextRequest("Extract the name");
        using var doc = JsonDocument.Parse("""{"type":"object","properties":{"name":{"type":"string"}}}""");
        request.Tools =
        [
            new AnthropicTool { Name = AnthropicProvider.StructuredToolName, InputSchema = doc.RootElement.Clone() },
        ];

        var response = await transport.SendAsync(new Sonnet46(), request, "GenerateAsync", CancellationToken.None);

        response.Content![0].Text.Should().Be("""{"name":"Ada"}""");

        var args = runner.LastInvocation!.Arguments;
        // The synthetic respond_json tool must NOT be forwarded to the CLI.
        args.Should().NotContain(AnthropicProvider.StructuredToolName);
        // The schema must be injected via --append-system-prompt.
        args.Should().Contain("--append-system-prompt");
        args.Should().Contain(a => a.Contains("JSON object") && a.Contains("\"type\""));
    }

    [Fact]
    public async Task SendAsync_ImageAttachment_NoApiKey_ThrowsNotSupported()
    {
        var runner = new FakeRunner();
        var transport = CreateTransport(runner, new AnthropicOptions { Transport = AnthropicTransport.Sdk }); // no ApiKey

        var request = TextRequest("Describe this");
        request.Messages[0].Content.Insert(0, new AnthropicContentBlock
        {
            Type = "image",
            Source = new AnthropicSource { Type = "base64", MediaType = "image/png", Data = "AAAA" },
        });

        var act = async () => await transport.SendAsync(new Sonnet46(), request, "GenerateAsync", CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
        runner.LastInvocation.Should().BeNull("the CLI must not be invoked for an unsupported request");
    }

    [Fact]
    public async Task SendAsync_ImageAttachment_WithApiKey_FallsBackToApiTransport()
    {
        var runner = new FakeRunner();
        var apiHandler = MockHandler("""{"id":"api_1","content":[{"type":"text","text":"FROM_API"}],"usage":{"input_tokens":1,"output_tokens":1}}""");
        var options = new AnthropicOptions { Transport = AnthropicTransport.Sdk, ApiKey = "sk-test" };
        var transport = CreateTransport(runner, options, apiHandler);

        var request = TextRequest("Describe this");
        request.Messages[0].Content.Insert(0, new AnthropicContentBlock
        {
            Type = "image",
            Source = new AnthropicSource { Type = "base64", MediaType = "image/png", Data = "AAAA" },
        });

        var response = await transport.SendAsync(new Sonnet46(), request, "GenerateAsync", CancellationToken.None);

        response.Content![0].Text.Should().Be("FROM_API");
        runner.LastInvocation.Should().BeNull("the request fell back to the HTTP API, not the CLI");
    }

    [Fact]
    public async Task SendAsync_FunctionTool_IsUnsupported_AndThrowsWithoutApiKey()
    {
        var runner = new FakeRunner();
        var transport = CreateTransport(runner, new AnthropicOptions { Transport = AnthropicTransport.Sdk });

        var request = TextRequest("What's the weather?");
        request.Tools =
        [
            new AnthropicTool { Name = "get_weather", Description = "weather", InputSchema = JsonDocument.Parse("{}").RootElement.Clone() },
        ];

        var act = async () => await transport.SendAsync(new Sonnet46(), request, "GenerateAsync", CancellationToken.None);

        (await act.Should().ThrowAsync<NotSupportedException>()).Which.Message.Should().Contain("get_weather");
    }

    [Fact]
    public async Task SendAsync_NonZeroExit_Throws()
    {
        var runner = new FakeRunner { ExitCode = 1, Stderr = "boom" };
        var transport = CreateTransport(runner, new AnthropicOptions { Transport = AnthropicTransport.Sdk });

        var act = async () => await transport.SendAsync(new Sonnet46(), TextRequest("hi"), "GenerateAsync", CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("boom");
    }

    [Fact]
    public async Task StreamAsync_AssistantLines_EmitsTextDeltas()
    {
        var runner = new FakeRunner
        {
            StreamLines =
            [
                """{"type":"system","subtype":"init"}""",
                """{"type":"assistant","message":{"content":[{"type":"text","text":"Hel"}]}}""",
                """{"type":"assistant","message":{"content":[{"type":"text","text":"lo"}]}}""",
                """{"type":"result","result":"Hello","session_id":"s"}""",
            ],
        };
        var transport = CreateTransport(runner, new AnthropicOptions { Transport = AnthropicTransport.Sdk });

        var collected = new List<string>();
        await foreach (var delta in transport.StreamAsync(new Sonnet46(), TextRequest("hi"), "StreamAsync", CancellationToken.None))
            collected.Add(delta);

        collected.Should().Equal("Hel", "lo");

        var args = runner.LastInvocation!.Arguments;
        args.Should().Contain("stream-json");
        args.Should().Contain("--verbose");
    }

    // ---- helpers --------------------------------------------------------------

    private static AnthropicCliTransport CreateTransport(
        FakeRunner runner,
        AnthropicOptions options,
        HttpMessageHandler? apiHandler = null)
    {
        // The locator runs during invocation building; point it at a file that exists
        // so discovery succeeds (the fake runner ignores the path).
        options.Cli.ExecutablePath = typeof(AnthropicCliTransportTests).Assembly.Location;

        var httpClient = new HttpClient(apiHandler ?? ThrowingHandler())
        {
            BaseAddress = new Uri("https://api.anthropic.com"),
        };
        var apiTransport = new AnthropicApiTransport(
            httpClient,
            Options.Create(options),
            NullLogger<AnthropicApiTransport>.Instance);

        return new AnthropicCliTransport(
            runner,
            apiTransport,
            Options.Create(options),
            Options.Create(new AiOptions()),
            NullLogger<AnthropicCliTransport>.Instance);
    }

    private static AnthropicMessagesRequest TextRequest(string text) => new()
    {
        Model = "claude-sonnet-4-6",
        MaxTokens = 1024,
        Messages =
        [
            new AnthropicMessageItem
            {
                Role = "user",
                Content = [new AnthropicContentBlock { Type = "text", Text = text }],
            },
        ],
    };

    private static HttpMessageHandler MockHandler(string responseJson)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
        return handler.Object;
    }

    private static HttpMessageHandler ThrowingHandler()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("API transport should not have been called."));
        return handler.Object;
    }

    private sealed class FakeRunner : IClaudeCliRunner
    {
        public ClaudeCliInvocation? LastInvocation;
        public string JsonStdout = """{"type":"result","result":"ok"}""";
        public string Stderr = "";
        public int ExitCode;
        public string[] StreamLines = [];

        public Task<ClaudeProcessResult> RunAsync(ClaudeCliInvocation invocation, CancellationToken cancellationToken)
        {
            LastInvocation = invocation;
            return Task.FromResult(new ClaudeProcessResult
            {
                ExitCode = ExitCode,
                StandardOutput = JsonStdout,
                StandardError = Stderr,
            });
        }

        public async IAsyncEnumerable<string> StreamLinesAsync(
            ClaudeCliInvocation invocation,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastInvocation = invocation;
            foreach (var line in StreamLines)
            {
                await Task.Yield();
                yield return line;
            }
        }
    }
}

/// <summary>Tests for the OS-aware Claude CLI binary discovery.</summary>
public class ClaudeCliLocatorTests
{
    [Fact]
    public void ResolveCore_ExplicitPathThatExists_IsReturned()
    {
        var resolved = ClaudeCliLocator.ResolveCore(
            explicitPath: "/opt/tools/claude",
            fileExists: p => p == "/opt/tools/claude",
            pathEnvironment: null,
            wellKnownDirectories: []);

        resolved.Should().Be("/opt/tools/claude");
    }

    [Fact]
    public void ResolveCore_ExplicitPathMissing_Throws()
    {
        var act = () => ClaudeCliLocator.ResolveCore(
            explicitPath: "/nope/claude",
            fileExists: _ => false,
            pathEnvironment: null,
            wellKnownDirectories: []);

        act.Should().Throw<FileNotFoundException>().Which.Message.Should().Contain("/nope/claude");
    }

    [Fact]
    public void ResolveCore_FindsExecutableOnPath()
    {
        var name = ClaudeCliLocator.ExecutableNames()[0]; // OS-appropriate (claude.exe / claude)
        var dir = OperatingSystem.IsWindows() ? @"C:\tools" : "/tools";
        var expected = Path.Combine(dir, name);

        var resolved = ClaudeCliLocator.ResolveCore(
            explicitPath: null,
            fileExists: p => p == expected,
            pathEnvironment: dir,
            wellKnownDirectories: []);

        resolved.Should().Be(expected);
    }

    [Fact]
    public void ResolveCore_NotFoundAnywhere_Throws()
    {
        var act = () => ClaudeCliLocator.ResolveCore(
            explicitPath: null,
            fileExists: _ => false,
            pathEnvironment: OperatingSystem.IsWindows() ? @"C:\a;C:\b" : "/a:/b",
            wellKnownDirectories: []);

        act.Should().Throw<FileNotFoundException>();
    }
}

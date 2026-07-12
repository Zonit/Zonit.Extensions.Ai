using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Zonit.Extensions.Ai.OpenAi;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Deterministic (no-network) guard for the OpenAI agent session. The OpenAI Responses
/// API does NOT persist <c>text.format</c> across <c>previous_response_id</c> chaining,
/// so a structured-output agent MUST resend the format on every continuation turn —
/// otherwise the post-tool-call final answer comes back as free-form text and fails to
/// parse into the typed result. Drives the public <see cref="OpenAiAgentAdapter"/> over a
/// two-response mock.
/// </summary>
public class OpenAiAgentSessionTests
{
    private const string FunctionCallResponse =
        """{"id":"resp_1","output":[{"type":"function_call","call_id":"call_1","name":"get_code","arguments":"{}"}],"usage":{"input_tokens":5,"output_tokens":2}}""";

    private const string FinalResponse =
        """{"id":"resp_2","output":[{"type":"message","content":[{"type":"output_text","text":"{\"code\":\"X\"}"}]}],"usage":{"input_tokens":5,"output_tokens":2}}""";

    [Fact]
    public async Task Continuation_WithStructuredOutput_ResendsTextFormat()
    {
        var requests = new List<string>();
        var responses = new Queue<string>([FunctionCallResponse, FinalResponse]);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (req.Content is not null)
                    requests.Add(req.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            })
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
            });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.openai.com") };
        var options = Options.Create(new OpenAiOptions { ApiKey = "test" });
        var adapter = new OpenAiAgentAdapter(httpClient, options, NullLogger<OpenAiAgentAdapter>.Instance);

        await using var session = adapter.BeginSession(new AgentSessionContext
        {
            Llm = new GPT4o(),
            Prompt = new StringPrompt { Text = "Get the code." },
            ResponseType = typeof(Answer),
            Tools = Array.Empty<ITool>(),
        });

        var turn1 = await session.RunTurnAsync(null, CancellationToken.None);
        turn1.ToolCalls.Should().ContainSingle();

        var toolResult = new ToolResult
        {
            CallId = turn1.ToolCalls[0].Id,
            Name = "get_code",
            Output = JsonDocument.Parse("""{"code":"X"}""").RootElement,
            IsError = false,
        };
        await session.RunTurnAsync([toolResult], CancellationToken.None);

        requests.Should().HaveCount(2);
        // Turn 1 carries text.format AND previous_response_id is absent.
        requests[0].Should().Contain("\"text\"");
        requests[0].Should().Contain("json_schema");
        // Turn 2 (continuation) chains via previous_response_id AND re-sends text.format.
        requests[1].Should().Contain("previous_response_id");
        requests[1].Should().Contain("\"text\"");
        requests[1].Should().Contain("\"format\"");
        requests[1].Should().Contain("json_schema");
    }

    [Fact]
    public async Task Continuation_ResendsTools()
    {
        var requests = new List<string>();
        var responses = new Queue<string>([FunctionCallResponse, FinalResponse]);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (req.Content is not null)
                    requests.Add(req.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            })
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
            });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.openai.com") };
        var options = Options.Create(new OpenAiOptions { ApiKey = "test" });
        var adapter = new OpenAiAgentAdapter(httpClient, options, NullLogger<OpenAiAgentAdapter>.Instance);

        await using var session = adapter.BeginSession(new AgentSessionContext
        {
            Llm = new GPT4o(),
            Prompt = new StringPrompt { Text = "Get the code." },
            ResponseType = typeof(Answer),
            Tools = new ITool[] { new FakeTool() },
        });

        var turn1 = await session.RunTurnAsync(null, CancellationToken.None);
        turn1.ToolCalls.Should().ContainSingle();

        var toolResult = new ToolResult
        {
            CallId = turn1.ToolCalls[0].Id,
            Name = "get_code",
            Output = JsonDocument.Parse("""{"code":"X"}""").RootElement,
            IsError = false,
        };
        await session.RunTurnAsync([toolResult], CancellationToken.None);

        requests.Should().HaveCount(2);
        // Turn 1 advertises the tool.
        requests[0].Should().Contain("\"tools\"");
        requests[0].Should().Contain("get_code");
        // Turn 2 (continuation) MUST re-advertise the tool. The Responses API does not
        // carry `tools` across previous_response_id chaining, so dropping it here strands
        // the model with no tools on turn 2+ — it stops after a single call and may even
        // claim "tools are unavailable" (the "OpenAI is not an agent" symptom).
        requests[1].Should().Contain("previous_response_id");
        requests[1].Should().Contain("\"tools\"");
        requests[1].Should().Contain("get_code");
    }

    private sealed class Answer
    {
        public string Code { get; init; } = "";
    }

    private sealed class FakeTool : ITool
    {
        public string Name => "get_code";
        public string Description => "Returns a code.";
        public JsonElement InputSchema { get; } =
            JsonDocument.Parse("""{"type":"object","properties":{},"additionalProperties":false}""").RootElement;
        public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
            => Task.FromResult(JsonDocument.Parse("""{"code":"X"}""").RootElement);
    }

    private sealed class StringPrompt : IPrompt
    {
        public required string Text { get; init; }
        public IReadOnlyList<Asset>? Files => null;
    }
}

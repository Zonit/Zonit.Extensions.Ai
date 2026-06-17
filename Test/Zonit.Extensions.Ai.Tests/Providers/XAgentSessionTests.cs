using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// The X (Grok) agent session is non-streaming, but shares the SAME contract as
/// every other provider: a turn with no usable content THROWS a typed
/// <see cref="AiEmptyResponseException"/> (never an empty Value), retrying the
/// data-loss case on the shared <c>AiOptions.Resilience</c> schedule. Drives the
/// public <see cref="XAgentAdapter"/> over a mocked HTTP response — no network.
/// </summary>
public class XAgentSessionTests
{
    private const string EmptyResponse =
        """{"id":"resp_empty","status":"completed","output":[],"usage":{"input_tokens":5,"output_tokens":0}}""";

    private const string TruncatedResponse =
        """{"id":"resp_trunc","status":"incomplete","incomplete_details":{"reason":"max_output_tokens"},"output":[],"usage":{"input_tokens":5,"output_tokens":100}}""";

    private const string RefusalResponse =
        """{"id":"resp_ref","status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"I can't help with that."}]}],"usage":{"input_tokens":5,"output_tokens":2}}""";

    private const string HealthyResponse =
        """{"id":"resp_ok","status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Hello"}]}],"usage":{"input_tokens":5,"output_tokens":3}}""";

    [Fact]
    public async Task RunTurnAsync_EmptyResponse_ThrowsEmptyAfterRetries()
    {
        await using var session = BeginSession(EmptyResponse, maxRetries: 0);

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<AiEmptyResponseException>()).Which;
        ex.Code.Should().Be(AiResponseError.EmptyAfterRetries);
        ex.Message.Should().Contain("[AI-E1001]");
    }

    [Fact]
    public async Task RunTurnAsync_Truncated_ThrowsTruncated_NotRetried()
    {
        // Retry budget intact, but max_output_tokens is deterministic → no retry.
        await using var session = BeginSession(TruncatedResponse, maxRetries: 6);

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<AiEmptyResponseException>()).Which;
        ex.Code.Should().Be(AiResponseError.Truncated);
        ex.Message.Should().Contain("[AI-E1002]");
    }

    [Fact]
    public async Task RunTurnAsync_Refusal_ThrowsRefusal_NotRetried()
    {
        await using var session = BeginSession(RefusalResponse, maxRetries: 6);

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<AiEmptyResponseException>()).Which;
        ex.Code.Should().Be(AiResponseError.Refusal);
        ex.Message.Should().Contain("[AI-E1003]");
    }

    [Fact]
    public async Task RunTurnAsync_HealthyResponse_ReturnsFinalText()
    {
        await using var session = BeginSession(HealthyResponse, maxRetries: 0);

        var turn = await session.RunTurnAsync(null, CancellationToken.None);

        turn.ToolCalls.Should().BeEmpty();
        turn.FinalText.Should().Be("Hello");
    }

    private static IAgentSession BeginSession(string jsonBody, int maxRetries)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.x.ai") };
        var xOptions = Options.Create(new XOptions { ApiKey = "test" });
        var ai = Options.Create(new AiOptions { Resilience = { MaxRetryAttempts = maxRetries } });

        var adapter = new XAgentAdapter(httpClient, xOptions, ai, NullLogger<XAgentAdapter>.Instance);

        return adapter.BeginSession(new AgentSessionContext
        {
            Llm = new Grok43(),
            Prompt = new StringPrompt { Text = "Produce the brief." },
            ResponseType = null,
            Tools = Array.Empty<ITool>(),
        });
    }

    private sealed class StringPrompt : IPrompt
    {
        public required string Text { get; init; }
        public IReadOnlyList<Asset>? Files => null;
    }
}

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Streaming agent-session behaviour: a turn that finishes with no actionable
/// content must THROW a typed <see cref="AiEmptyResponseException"/> (never
/// surface an empty Value), and a healthy turn must still parse its text.
/// Drives the public <see cref="AnthropicAgentAdapter"/> over a mocked SSE
/// stream — no network.
/// </summary>
public class AnthropicAgentSessionTests
{
    // SSE stream: model emitted only a `thinking` block then ended the turn —
    // the server-side data-loss symptom (anthropic-sdk-typescript#867).
    private const string EmptyThinkingTurnSse =
        "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_empty\",\"usage\":{\"input_tokens\":12,\"output_tokens\":0}}}\n" +
        "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"thinking\",\"thinking\":\"\"}}\n" +
        "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"thinking_delta\",\"thinking\":\"considering\"}}\n" +
        "data: {\"type\":\"content_block_stop\",\"index\":0}\n" +
        "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":0}}\n" +
        "data: {\"type\":\"message_stop\"}\n";

    private const string RefusalTurnSse =
        "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_ref\",\"usage\":{\"input_tokens\":7,\"output_tokens\":0}}}\n" +
        "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"refusal\"},\"usage\":{\"output_tokens\":0}}\n" +
        "data: {\"type\":\"message_stop\"}\n";

    private const string HealthyTextTurnSse =
        "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_ok\",\"usage\":{\"input_tokens\":5,\"output_tokens\":3}}}\n" +
        "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n" +
        "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}\n" +
        "data: {\"type\":\"content_block_stop\",\"index\":0}\n" +
        "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":3}}\n" +
        "data: {\"type\":\"message_stop\"}\n";

    [Fact]
    public async Task RunTurnAsync_EmptyThinkingTurn_ThrowsEmptyAfterRetries()
    {
        // MaxRetryAttempts=0 → throw on the first empty turn (no backoff wait).
        await using var session = BeginSession(EmptyThinkingTurnSse, maxRetries: 0);

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<AiEmptyResponseException>()).Which;
        ex.Code.Should().Be(AiResponseError.EmptyAfterRetries);
        ex.StopReason.Should().Be("end_turn");
        ex.Attempts.Should().Be(1);
        ex.Message.Should().Contain("[AI-E1001]");
    }

    [Fact]
    public async Task RunTurnAsync_Refusal_ThrowsRefusal_AndIsNotRetried()
    {
        // Retry budget (6) is intact, yet refusal must NOT be retried —
        // it is deterministic given the same request. It throws immediately.
        await using var session = BeginSession(RefusalTurnSse, maxRetries: 6);

        var act = async () => await session.RunTurnAsync(null, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<AiEmptyResponseException>()).Which;
        ex.Code.Should().Be(AiResponseError.Refusal);
        ex.Attempts.Should().Be(1, "refusal is surfaced on the first turn, never retried");
        ex.Message.Should().Contain("[AI-E1003]");
    }

    [Fact]
    public async Task RunTurnAsync_HealthyTextTurn_ReturnsFinalText()
    {
        await using var session = BeginSession(HealthyTextTurnSse, maxRetries: 0);

        var turn = await session.RunTurnAsync(null, CancellationToken.None);

        turn.ToolCalls.Should().BeEmpty();
        turn.FinalText.Should().Be("Hello");
    }

    private static IAgentSession BeginSession(string sseBody, int maxRetries)
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
                Content = new StringContent(sseBody, Encoding.UTF8, "text/event-stream"),
            });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.anthropic.com") };
        var anthropic = Options.Create(new AnthropicOptions { ApiKey = "test" });
        // Shared retry budget lives on AiOptions.Resilience — same knob the HTTP layer uses.
        var ai = Options.Create(new AiOptions { Resilience = { MaxRetryAttempts = maxRetries } });

        // Transport defaults to Api → the adapter routes to the HTTP session; an empty
        // service provider is sufficient (no CLI runner / bridge needed on this path).
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var adapter = new AnthropicAgentAdapter(httpClient, serviceProvider, anthropic, ai, NullLogger<AnthropicAgentAdapter>.Instance);

        return adapter.BeginSession(new AgentSessionContext
        {
            Llm = new Opus48(),
            Prompt = new StringPrompt { Text = "Produce the brief." },
            ResponseType = null,                  // free-form text
            Tools = Array.Empty<ITool>(),
        });
    }

    private sealed class StringPrompt : IPrompt
    {
        public required string Text { get; init; }
        public IReadOnlyList<Asset>? Files => null;
    }
}

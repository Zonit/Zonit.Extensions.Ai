using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// An assistant message is a <b>sequence</b> of blocks, not a single text — and every provider
/// splits it once a server-side tool runs. These tests pin that the whole message comes back.
/// </summary>
/// <remarks>
/// The regression they guard is not hypothetical: Anthropic's <c>GenerateAsync</c> read only the
/// first text block, so a web-search answer came back as its preamble ("I'll search for this.")
/// with the researched answer silently discarded — and even with no preamble, one fragment of a
/// dozen. The agent loop had always concatenated, which is why the same prompt worked there and
/// made the fault look like a missing tool-continuation loop rather than lost text.
/// </remarks>
public class MultiBlockAnswerTests
{
    // Shaped like a real Anthropic web-search response: preamble, the server tool, its result,
    // then the answer spread over several text blocks (that is how citations arrive).
    private const string AnthropicWebSearchResponse = """
        {
          "id": "msg_1",
          "stop_reason": "end_turn",
          "content": [
            {"type":"text","text":"I'll search for this."},
            {"type":"server_tool_use","id":"srvtoolu_1","name":"web_search"},
            {"type":"web_search_tool_result","tool_use_id":"srvtoolu_1"},
            {"type":"text","text":"Brent crude is trading at $109.21"},
            {"type":"text","text":" per barrel"},
            {"type":"text","text":", up 3.34% on the day."}
          ],
          "usage": {"input_tokens": 10, "output_tokens": 40}
        }
        """;

    [Fact]
    public async Task Anthropic_WithServerSideWebSearch_ReturnsTheWholeAnswer_NotJustThePreamble()
    {
        var provider = CreateAnthropicProvider(AnthropicWebSearchResponse);

        var result = await provider.GenerateAsync(
            new Sonnet5(), new TestPrompt { Text = "Brent price?" }, CancellationToken.None);

        result.Value.Should().Be(
            "I'll search for this.Brent crude is trading at $109.21 per barrel, up 3.34% on the day.");
    }

    [Fact]
    public async Task Anthropic_SingleTextBlock_IsUnchanged()
    {
        var provider = CreateAnthropicProvider(
            """{"id":"msg_1","content":[{"type":"text","text":"Hello"}],"usage":{"input_tokens":1,"output_tokens":1}}""");

        var result = await provider.GenerateAsync(
            new Sonnet5(), new TestPrompt { Text = "Hi" }, CancellationToken.None);

        result.Value.Should().Be("Hello");
    }

    [Fact]
    public async Task Anthropic_ThinkingBlocks_AreNotPartOfTheAnswer()
    {
        // Only `text` blocks are the message; `thinking` is not something the caller asked for.
        var provider = CreateAnthropicProvider("""
            {"id":"msg_1","content":[
               {"type":"thinking","thinking":"internal reasoning"},
               {"type":"text","text":"The answer."}],
             "usage":{"input_tokens":1,"output_tokens":1}}
            """);

        var result = await provider.GenerateAsync(
            new Sonnet5(), new TestPrompt { Text = "Hi" }, CancellationToken.None);

        result.Value.Should().Be("The answer.");
    }

    [Fact]
    public async Task OpenAi_WithServerSideWebSearch_ReadsEveryMessageItem()
    {
        // The Responses API interleaves reasoning / web_search_call items with messages, and a
        // model that searches twice can emit more than one message.
        var handler = new QueuedHandler(ResponsesSse.FromResponseJson("""
            {"id":"resp_1","status":"completed","output":[
               {"type":"reasoning"},
               {"type":"web_search_call"},
               {"type":"message","content":[{"type":"output_text","text":"Brent is $109.21"}]},
               {"type":"web_search_call"},
               {"type":"message","content":[{"type":"output_text","text":", up 3.34% today."}]}],
             "usage":{"input_tokens":10,"output_tokens":40}}
            """));

        var provider = new OpenAiProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com") },
            Options.Create(new OpenAiOptions { ApiKey = "test" }),
            NullLogger<OpenAiProvider>.Instance);

        var result = await provider.GenerateAsync(
            new Luna56(), new TestPrompt { Text = "Brent price?" }, CancellationToken.None);

        result.Value.Should().Be("Brent is $109.21, up 3.34% today.");
    }

    [Fact]
    public async Task Grok_WithServerSideWebSearch_ReadsEveryMessageItem()
    {
        var handler = new QueuedHandler(ResponsesSse.FromResponseJson("""
            {"id":"resp_1","status":"completed","output":[
               {"type":"reasoning"},
               {"type":"web_search_call"},
               {"type":"message","content":[{"type":"output_text","text":"Brent is $109.21"}]},
               {"type":"message","content":[{"type":"output_text","text":", up 3.34% today."}]}],
             "usage":{"input_tokens":10,"output_tokens":40}}
            """));

        var provider = new XProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai") },
            Options.Create(new XOptions { ApiKey = "test" }),
            NullLogger<XProvider>.Instance);

        var result = await provider.GenerateAsync(
            new Grok46(), new TestPrompt { Text = "Brent price?" }, CancellationToken.None);

        result.Value.Should().Be("Brent is $109.21, up 3.34% today.");
    }

    private static AnthropicProvider CreateAnthropicProvider(string bufferedResponseJson)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    AnthropicSse.FromResponseJson(bufferedResponseJson), Encoding.UTF8, "text/event-stream"),
            });

        var options = Options.Create(new AnthropicOptions { ApiKey = "test" });
        var transport = new AnthropicApiTransport(
            new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://api.anthropic.com") },
            options,
            Options.Create(new AiOptions()),
            NullLogger<AnthropicApiTransport>.Instance);

        // The provider resolves its transport from the container (see AnthropicProviderTests).
        return new AnthropicProvider(new SingleTransportProvider(transport), NullLogger<AnthropicProvider>.Instance);
    }

    private sealed class SingleTransportProvider(IAnthropicTransport transport) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IAnthropicTransport) ? transport : null;
    }

    private sealed class QueuedHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
            });
    }

    private sealed class TestPrompt : IPrompt<string>
    {
        public string? System { get; set; }
        public required string Text { get; set; }
        public IReadOnlyList<Asset>? Files { get; set; }
    }
}

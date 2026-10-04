using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Deterministic guard for the shared Responses-API SSE assembler (OpenAI and xAI). Their
/// single-shot paths and agent loops all stream on the wire and reassemble the reply, so this
/// is the seam where a truncated or faulty stream has to become an error rather than a
/// half-answer.
/// </summary>
public class ResponsesStreamAssemblerTests
{
    [Fact]
    public async Task ReadAsync_ReturnsTheTerminalResponseObject_Verbatim()
    {
        const string body = """
            data: {"type":"response.created","response":{"id":"resp_1","status":"in_progress"}}

            data: {"type":"response.output_text.delta","delta":"Hel"}

            data: {"type":"response.output_text.delta","delta":"lo"}

            data: {"type":"response.completed","response":{"id":"resp_1","status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Hello"}]}],"usage":{"input_tokens":3,"output_tokens":2}}}

            """;

        var json = await ReadAsync(body);

        json.Should().Contain("\"status\":\"completed\"");
        json.Should().Contain("\"text\":\"Hello\"");
        json.Should().Contain("\"output_tokens\":2");
    }

    [Fact]
    public async Task ReadAsync_WhenTheStreamEndsEarly_Throws()
    {
        // No terminal event: the generation was cut off. Returning what arrived would hand
        // the caller a plausible-looking half answer.
        const string body = """
            data: {"type":"response.created","response":{"id":"resp_1"}}

            data: {"type":"response.output_text.delta","delta":"half an ans"}

            """;

        var act = () => ReadAsync(body);

        (await act.Should().ThrowAsync<HttpRequestException>())
            .WithMessage("*before a terminal response event*");
    }

    [Fact]
    public async Task ReadAsync_OnAnErrorEvent_ThrowsWithTheProvidersMessage()
    {
        const string body = """
            data: {"type":"response.created","response":{"id":"resp_1"}}

            data: {"type":"error","code":"server_error","message":"upstream exploded"}

            """;

        var act = () => ReadAsync(body);

        (await act.Should().ThrowAsync<HttpRequestException>())
            .WithMessage("*upstream exploded*");
    }

    [Fact]
    public async Task ReadAsync_WhenTheStreamGoesSilent_TimesOutWithAnActionableMessage()
    {
        // A stalled server that keeps the socket open: nothing arrives, and without the
        // watchdog the read would block forever (the resilience pipeline is out of scope
        // once the response headers have been read).
        using var stalled = new StalledStream();
        using var reader = new StreamReader(stalled, Encoding.UTF8);

        var act = () => ResponsesStreamAssembler.ReadAsync(
            reader, TimeSpan.FromMilliseconds(150), "OpenAI", "GenerateAsync", CancellationToken.None);

        (await act.Should().ThrowAsync<TimeoutException>())
            .WithMessage("*InterEventTimeout*");
    }

    [Fact]
    public async Task ReadAsync_WhenTheStreamFreezesMidAnswer_NamesTheLastEvent()
    {
        // The Sol 6.1 hang: text was streaming, then nothing. The timeout says where it stopped.
        using var stream = new ScriptedStream(stallAtEnd: true,
            (TimeSpan.Zero, Created + MessageItemAdded + Delta("Gold settled at ")));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var act = () => ResponsesStreamAssembler.ReadAsync(
            reader, TimeSpan.FromMilliseconds(150), "OpenAI", "GenerateAsync", CancellationToken.None);

        (await act.Should().ThrowAsync<TimeoutException>().WaitAsync(TimeSpan.FromSeconds(10)))
            .WithMessage("OpenAI GenerateAsync stream produced no event for 0.2 s after response.output_text.delta at * s*InterEventTimeout*");
    }

    [Fact]
    public async Task ReadAsync_WhenNothingArrivesBeforeTheFirstOutput_TimesOut()
    {
        // Issue #30: silence before any output item is caught by the same limit.
        using var stream = new ScriptedStream(stallAtEnd: true, (TimeSpan.Zero, Created + ReasoningItemAdded));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var act = () => ResponsesStreamAssembler.ReadAsync(
            reader, TimeSpan.FromMilliseconds(150), "OpenAI", "GenerateAsync", CancellationToken.None);

        (await act.Should().ThrowAsync<TimeoutException>().WaitAsync(TimeSpan.FromSeconds(10)))
            .WithMessage("*after response.output_item.added (reasoning) at *");
    }

    [Fact]
    public async Task ReadAsync_WhenCompletedNeverFollowsTheAnswer_TimesOut()
    {
        // Issue #30: the answer is done and only response.completed is left.
        using var stream = new ScriptedStream(stallAtEnd: true,
            (TimeSpan.Zero, MessageItemAdded + Delta("Hello") + MessageItemDone));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var act = () => ResponsesStreamAssembler.ReadAsync(
            reader, TimeSpan.FromMilliseconds(150), "OpenAI", "GenerateAsync", CancellationToken.None);

        (await act.Should().ThrowAsync<TimeoutException>().WaitAsync(TimeSpan.FromSeconds(10)))
            .WithMessage("*after response.output_item.done (message) at *");
    }

    [Fact]
    public async Task ReadAsync_ToleratesGapsShorterThanTheLimit()
    {
        using var stream = new ScriptedStream(stallAtEnd: false,
            (TimeSpan.Zero, Created + ReasoningItemAdded),
            (TimeSpan.FromMilliseconds(300), ReasoningItemDone + MessageItemAdded + Delta("Hello")),
            (TimeSpan.FromMilliseconds(300), MessageItemDone + Completed("Hello")));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var json = await ResponsesStreamAssembler.ReadAsync(
            reader, TimeSpan.FromSeconds(2), "OpenAI", "GenerateAsync", CancellationToken.None);

        json.Should().Contain("\"text\":\"Hello\"");
    }

    [Fact]
    public async Task SendAsync_ReissuesARequestThatNeverStartsAnswering()
    {
        var handler = new SequenceHandler(
            () => new ScriptedStream(stallAtEnd: true, (TimeSpan.Zero, Created)),
            () => new ScriptedStream(stallAtEnd: false, (TimeSpan.Zero, Created + Completed("Hello"))));

        var json = await SendAsync(handler, maxRetries: 2).WaitAsync(TimeSpan.FromSeconds(10));

        json.Should().Contain("\"text\":\"Hello\"");
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public void InterEventTimeout_IsTheOneStallLimit_DefaultTenMinutes()
    {
        // One limit for every phase and provider: healthy gaps are seconds long everywhere (15 s
        // was the longest measured, gpt-6.1-sol at xhigh), while a slow server delays any frame.
        new AiResilienceOptions().InterEventTimeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task SendAsync_ReissuesTheRequest_WhenTheStreamFreezesMidAnswer()
    {
        var handler = new SequenceHandler(
            () => new ScriptedStream(stallAtEnd: true, (TimeSpan.Zero, MessageItemAdded + Delta("half an ans"))),
            () => new ScriptedStream(stallAtEnd: false,
                (TimeSpan.Zero, MessageItemAdded + Delta("Hello") + MessageItemDone + Completed("Hello"))));

        var json = await SendAsync(handler, maxRetries: 2);

        json.Should().Contain("\"text\":\"Hello\"");
        handler.Calls.Should().Be(2, "the frozen stream is re-issued once and the second attempt completes");
    }

    [Fact]
    public async Task SendAsync_ReissuesTheRequest_WhenTheStreamEndsWithoutATerminalEvent()
    {
        var handler = new SequenceHandler(
            () => new ScriptedStream(stallAtEnd: false, (TimeSpan.Zero, MessageItemAdded + Delta("half"))),
            () => new ScriptedStream(stallAtEnd: false, (TimeSpan.Zero, Completed("Hello"))));

        var json = await SendAsync(handler, maxRetries: 2);

        json.Should().Contain("\"text\":\"Hello\"");
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task SendAsync_WhenRetriesRunOut_SurfacesTheOriginalStall()
    {
        var handler = new SequenceHandler(
            () => new ScriptedStream(stallAtEnd: true, (TimeSpan.Zero, MessageItemAdded + Delta("a"))),
            () => new ScriptedStream(stallAtEnd: true, (TimeSpan.Zero, MessageItemAdded + Delta("a"))));

        var act = () => SendAsync(handler, maxRetries: 1);

        (await act.Should().ThrowAsync<TimeoutException>()).WithMessage("*InterEventTimeout*");
        handler.Calls.Should().Be(2, "one attempt plus one retry");
    }

    [Fact]
    public async Task SendAsync_DoesNotRetryAnErrorEvent()
    {
        // An `error` frame is the server's answer, not a broken stream.
        var handler = new SequenceHandler(
            () => new ScriptedStream(stallAtEnd: false,
                (TimeSpan.Zero, "data: {\"type\":\"error\",\"code\":\"invalid_prompt\",\"message\":\"bad\"}\n\n")));

        var act = () => SendAsync(handler, maxRetries: 3);

        (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage("*bad*");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_DoesNotRetryAfterTheCallerCancels()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var handler = new SequenceHandler(
            () => new ScriptedStream(stallAtEnd: true, (TimeSpan.Zero, MessageItemAdded)));

        var act = () => SendAsync(handler, maxRetries: 3, interEvent: TimeSpan.FromSeconds(30), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.Calls.Should().Be(1);
    }

    [Theory]
    // The one case that must trigger the buffered fallback: an unverified organization.
    [InlineData(HttpStatusCode.BadRequest,
        """{"error":{"message":"Your organization must be verified to stream this model","param":"stream","code":"unsupported_value"}}""",
        true)]
    [InlineData(HttpStatusCode.BadRequest,
        """{"error":{"message":"Streaming is not supported for this model","param":"stream"}}""",
        true)]
    // Ordinary 400s must keep surfacing as errors instead of being silently retried buffered.
    [InlineData(HttpStatusCode.BadRequest,
        """{"error":{"message":"Invalid schema for response_format","param":"text.format"}}""",
        false)]
    [InlineData(HttpStatusCode.TooManyRequests,
        """{"error":{"message":"Rate limit reached"}}""",
        false)]
    public void IsStreamingRejection_OnlyMatchesAStreamingRefusal(HttpStatusCode status, string body, bool expected)
        => ResponsesApiTransport.IsStreamingRejection(status, body).Should().Be(expected);

    private static async Task<string> ReadAsync(string sseBody)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseBody));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return await ResponsesStreamAssembler.ReadAsync(
            reader, TimeSpan.FromSeconds(5), "OpenAI", "GenerateAsync", CancellationToken.None);
    }

    private static Task<string> SendAsync(
        SequenceHandler handler, int maxRetries, TimeSpan? interEvent = null, CancellationToken cancellationToken = default)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var resilience = new AiResilienceOptions
        {
            InterEventTimeout = interEvent ?? TimeSpan.FromMilliseconds(150),
            MaxRetryAttempts = maxRetries,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
            RetryMaxDelay = TimeSpan.FromMilliseconds(1),
        };

        return ResponsesApiTransport.SendAsync(
            http, "/v1/responses", _ => "{}", resilience, "OpenAI", "GenerateAsync",
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, cancellationToken);
    }

    private const string Created = "data: {\"type\":\"response.created\",\"response\":{\"id\":\"resp_1\",\"status\":\"in_progress\"}}\n\n";
    private const string ReasoningItemAdded ="data: {\"type\":\"response.output_item.added\",\"item\":{\"type\":\"reasoning\"}}\n\n";
    private const string ReasoningItemDone = "data: {\"type\":\"response.output_item.done\",\"item\":{\"type\":\"reasoning\"}}\n\n";
    private const string MessageItemAdded = "data: {\"type\":\"response.output_item.added\",\"item\":{\"type\":\"message\"}}\n\n";
    private const string MessageItemDone = "data: {\"type\":\"response.output_item.done\",\"item\":{\"type\":\"message\"}}\n\n";

    private static string Delta(string text)
        => "data: {\"type\":\"response.output_text.delta\",\"delta\":\"" + text + "\"}\n\n";

    private static string Completed(string text)
        => "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"resp_1\",\"status\":\"completed\","
            + "\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"" + text + "\"}]}]}}\n\n";

    /// <summary>Answers each request with the next scripted SSE body, counting the calls.</summary>
    private sealed class SequenceHandler(params Func<Stream>[] bodies) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = bodies[Math.Min(Calls, bodies.Length - 1)]();
            Calls++;
            var content = new StreamContent(body);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    /// <summary>
    /// Delivers SSE chunks after scripted pauses, then either ends or freezes like a server that
    /// keeps the socket open but sends nothing more.
    /// </summary>
    private sealed class ScriptedStream(bool stallAtEnd, params (TimeSpan Delay, string Text)[] chunks) : Stream
    {
        private int _next;
        private byte[] _pending = [];
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_offset >= _pending.Length)
            {
                if (_next >= chunks.Length)
                {
                    if (!stallAtEnd) return 0;
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                var (delay, text) = chunks[_next++];
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);
                _pending = Encoding.UTF8.GetBytes(text);
                _offset = 0;
            }

            var n = Math.Min(buffer.Length, _pending.Length - _offset);
            _pending.AsMemory(_offset, n).CopyTo(buffer);
            _offset += n;
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A stream that never produces a byte and never ends — a frozen server.</summary>
    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

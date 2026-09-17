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

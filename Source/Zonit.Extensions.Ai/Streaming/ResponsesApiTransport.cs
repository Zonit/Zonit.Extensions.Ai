using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Ai;

/// <summary>
/// One <c>POST /v1/responses</c>, streamed on the wire and handed back as the finished
/// response body. Shared by the single-shot provider paths and the agent loops of every
/// provider that speaks the Responses API — OpenAI and xAI today.
/// </summary>
/// <remarks>
/// Streaming is the default because the buffered form holds one HTTP response open for the
/// entire generation: with <c>max_output_tokens</c> defaulting to the model's full capacity, a
/// large answer can outlive <c>Ai:Resilience:AttemptTimeout</c>, and the retry then restarts
/// generation from zero at full cost. Frames arriving continuously make liveness — not total
/// duration — the thing being measured. See <see cref="ResponsesStreamAssembler"/>.
/// </remarks>
public static class ResponsesApiTransport
{
    /// <summary>
    /// Providers whose API has refused <c>stream: true</c>, keyed by provider name; those send
    /// buffered requests for the rest of the process.
    /// </summary>
    /// <remarks>
    /// Process-wide (and never reset) on purpose: whether streaming is allowed is a property of
    /// the <i>account</i>, not of a request, a provider instance or an agent session — and those
    /// are all transient, so a per-instance flag would re-pay the failed round trip on every
    /// call. Keyed per provider so one account's restriction cannot mute another provider's
    /// streaming. It only ever flips on an explicit rejection; restoring streaming (e.g. after
    /// verifying an OpenAI organization) takes a restart.
    /// </remarks>
    private static readonly ConcurrentDictionary<string, bool> StreamingRejected = new(StringComparer.Ordinal);

    /// <summary>
    /// Sends the request and returns the terminal Response object as JSON — assembled from the
    /// SSE frames, or read straight off a buffered body when streaming is unavailable. Either
    /// way the caller receives exactly what the non-streaming endpoint would have returned.
    /// </summary>
    /// <param name="httpClient">Typed client, carrying the streaming resilience pipeline.</param>
    /// <param name="requestPath">Endpoint path, e.g. <c>/v1/responses</c>.</param>
    /// <param name="payloadFactory">
    /// Builds the request JSON for the given streaming mode — called again with <c>false</c> if
    /// the API turns out to reject streaming. Keeps provider DTOs out of this shared code.
    /// </param>
    /// <param name="interEventTimeout">Dead-stream watchdog; see <see cref="AiSseReader"/>.</param>
    /// <param name="provider">Provider name, for diagnostics and for the streaming-support flag.</param>
    /// <param name="operation">Calling operation, for diagnostics.</param>
    /// <param name="logger">Logger for the request payload and errors.</param>
    /// <param name="cancellationToken">Caller's token.</param>
    public static async Task<string> SendAsync(
        HttpClient httpClient,
        string requestPath,
        Func<bool, string> payloadFactory,
        TimeSpan interEventTimeout,
        string provider,
        string operation,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var streaming = !StreamingRejected.GetValueOrDefault(provider);

        var payload = payloadFactory(streaming);
        logger.LogDebug("{Provider} {Operation} request: {Payload}", provider, operation, payload);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestPath) { Content = content };
        using var response = await httpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Errors arrive as a normal buffered body, before any frame.
            var errorJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (streaming && IsStreamingRejection(response.StatusCode, errorJson))
            {
                StreamingRejected[provider] = true;
                logger.LogWarning(
                    "{Provider} rejected stream:true ({Status}) — falling back to buffered requests for the rest "
                    + "of this process: {Response}. Streaming can require a verified organization; without it a "
                    + "long generation may outlive Ai:Resilience:AttemptTimeout and be retried from zero.",
                    provider, response.StatusCode, errorJson);

                return await SendAsync(
                        httpClient, requestPath, payloadFactory, interEventTimeout,
                        provider, operation, logger, cancellationToken)
                    .ConfigureAwait(false);
            }

            logger.LogError("{Provider} {Operation} error: {Status} - {Response}",
                provider, operation, response.StatusCode, errorJson);
            throw new HttpRequestException($"{provider} API failed: {response.StatusCode}: {errorJson}");
        }

        if (!streaming)
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return await ResponsesStreamAssembler
            .ReadAsync(reader, interEventTimeout, provider, operation, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Opens a streamed response and hands back the reader, for the live paths that emit
    /// fragments as they arrive rather than assembling them. The caller owns the returned
    /// <see cref="StreamedResponse"/> and must dispose it.
    /// </summary>
    public static async Task<StreamedResponse> OpenStreamAsync(
        HttpClient httpClient,
        string requestPath,
        string payload,
        string provider,
        string operation,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{Provider} {Operation} request: {Payload}", provider, operation, payload);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestPath) { Content = content };
        var response = await httpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            var errorJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response.Dispose();

            logger.LogError("{Provider} {Operation} error: {Status} - {Response}",
                provider, operation, status, errorJson);
            throw new HttpRequestException($"{provider} API failed: {status}: {errorJson}");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new StreamedResponse(response, stream);
    }

    /// <summary>
    /// Recognises "this account may not stream" — OpenAI gates streaming on organization
    /// verification for several model families and answers an unverified org with a 400 naming
    /// the <c>stream</c> parameter. Deliberately narrow: any other 4xx is a real error and must
    /// keep surfacing as one rather than being silently retried without streaming.
    /// </summary>
    public static bool IsStreamingRejection(HttpStatusCode status, string body)
    {
        if (status is not (HttpStatusCode.BadRequest or HttpStatusCode.Forbidden))
            return false;

        var mentionsStream = body.Contains("\"stream\"", StringComparison.OrdinalIgnoreCase)
            || body.Contains("streaming", StringComparison.OrdinalIgnoreCase);

        return mentionsStream
            && (body.Contains("must be verified", StringComparison.OrdinalIgnoreCase)
                || body.Contains("unsupported_value", StringComparison.OrdinalIgnoreCase)
                || body.Contains("unsupported_parameter", StringComparison.OrdinalIgnoreCase)
                || body.Contains("not supported", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// An open streamed response and its reader, disposed together.
    /// </summary>
    public sealed class StreamedResponse : IDisposable
    {
        private readonly HttpResponseMessage _response;
        private readonly Stream _stream;

        internal StreamedResponse(HttpResponseMessage response, Stream stream)
        {
            _response = response;
            _stream = stream;
            Reader = new StreamReader(stream, Encoding.UTF8);
        }

        /// <summary>Reader over the SSE body.</summary>
        public StreamReader Reader { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            Reader.Dispose();
            _stream.Dispose();
            _response.Dispose();
        }
    }
}

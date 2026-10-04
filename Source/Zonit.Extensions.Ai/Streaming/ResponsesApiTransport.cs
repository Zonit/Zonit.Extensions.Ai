using System.Collections.Concurrent;
using System.Net;
using System.Runtime.ExceptionServices;
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
    public static Task<string> SendAsync(
        HttpClient httpClient,
        string requestPath,
        Func<bool, string> payloadFactory,
        TimeSpan interEventTimeout,
        string provider,
        string operation,
        ILogger logger,
        CancellationToken cancellationToken = default)
        => SendWithRetryAsync(
            httpClient, requestPath, payloadFactory, interEventTimeout, TimeSpan.Zero,
            maxRetries: 0, retryDelay: static _ => TimeSpan.Zero,
            provider, operation, logger, cancellationToken);

    /// <summary>
    /// Sends the request and returns the terminal Response object as JSON, re-issuing it when the
    /// stream stalls or breaks before the terminal event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Polly's retry covers only what happens before the response headers arrive; once a
    /// <c>200</c> has come back and the body is streaming, a stream that goes silent or drops
    /// is invisible to it. This loop covers that window: a watchdog timeout
    /// (<see cref="AiResilienceOptions.InterEventTimeout"/> while the model thinks,
    /// <see cref="AiResilienceOptions.OutputStallTimeout"/> while it writes) or a connection
    /// that ends mid-stream re-issues the identical request, on the shared schedule
    /// (<see cref="AiResilienceOptions.MaxRetryAttempts"/> + <see cref="AiResilienceOptions.RetryDelay"/>).
    /// </para>
    /// <para>
    /// A retry starts generation from zero and is billed again — the price of an answer, set
    /// against waiting on a stream that will never finish. An <c>error</c> event, an HTTP error
    /// status and the caller's cancellation are not retried here.
    /// </para>
    /// </remarks>
    /// <param name="httpClient">Typed client, carrying the streaming resilience pipeline.</param>
    /// <param name="requestPath">Endpoint path, e.g. <c>/v1/responses</c>.</param>
    /// <param name="payloadFactory">Builds the request JSON for the given streaming mode.</param>
    /// <param name="resilience">Watchdog limits and retry schedule — <c>Ai:Resilience</c>.</param>
    /// <param name="provider">Provider name, for diagnostics and for the streaming-support flag.</param>
    /// <param name="operation">Calling operation, for diagnostics.</param>
    /// <param name="logger">Logger for the request payload, retries and errors.</param>
    /// <param name="cancellationToken">Caller's token.</param>
    public static Task<string> SendAsync(
        HttpClient httpClient,
        string requestPath,
        Func<bool, string> payloadFactory,
        AiResilienceOptions resilience,
        string provider,
        string operation,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resilience);

        return SendWithRetryAsync(
            httpClient, requestPath, payloadFactory,
            resilience.InterEventTimeout > TimeSpan.Zero ? resilience.InterEventTimeout : TimeSpan.FromMinutes(30),
            resilience.OutputStallTimeout,
            Math.Max(0, resilience.MaxRetryAttempts),
            resilience.RetryDelay,
            provider, operation, logger, cancellationToken);
    }

    private static async Task<string> SendWithRetryAsync(
        HttpClient httpClient,
        string requestPath,
        Func<bool, string> payloadFactory,
        TimeSpan interEventTimeout,
        TimeSpan outputStallTimeout,
        int maxRetries,
        Func<int, TimeSpan> retryDelay,
        string provider,
        string operation,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await SendOnceAsync(
                        httpClient, requestPath, payloadFactory, interEventTimeout, outputStallTimeout,
                        provider, operation, logger, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (BrokenStreamException broken)
            {
                var cause = broken.InnerException!;
                if (attempt >= maxRetries || cancellationToken.IsCancellationRequested)
                    ExceptionDispatchInfo.Throw(cause);

                var delay = retryDelay(attempt + 1);
                logger.LogWarning(
                    "{Provider} {Operation}: the stream broke before the response completed ({Error}: {Message}). "
                    + "Re-issuing the request in {Delay} (retry {Attempt}/{Max}); generation restarts from zero.",
                    provider, operation, cause.GetType().Name, cause.Message, delay, attempt + 1, maxRetries);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// A streamed body that stopped short of its terminal event: the watchdog fired, the
    /// connection dropped mid-body, or the body ended without <c>response.completed</c>. All three
    /// leave no usable answer and are transient in practice. Only failures <i>while reading the
    /// body</i> count — a failure before the headers is Polly's to retry, and retrying it here
    /// too would multiply the attempts.
    /// </summary>
    internal static bool IsBrokenStream(Exception ex) => ex switch
    {
        TimeoutException => true,
        HttpRequestException { HttpRequestError: HttpRequestError.ResponseEnded } => true,
        IOException => true,
        _ => false,
    };

    /// <summary>Marks a body-phase failure as retryable; never escapes this class.</summary>
    private sealed class BrokenStreamException(Exception inner) : Exception(inner.Message, inner);

    private static async Task<string> SendOnceAsync(
        HttpClient httpClient,
        string requestPath,
        Func<bool, string> payloadFactory,
        TimeSpan interEventTimeout,
        TimeSpan outputStallTimeout,
        string provider,
        string operation,
        ILogger logger,
        CancellationToken cancellationToken)
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

                return await SendOnceAsync(
                        httpClient, requestPath, payloadFactory, interEventTimeout, outputStallTimeout,
                        provider, operation, logger, cancellationToken)
                    .ConfigureAwait(false);
            }

            logger.LogError("{Provider} {Operation} error: {Status} - {Response}",
                provider, operation, response.StatusCode, errorJson);
            throw new HttpRequestException($"{provider} API failed: {response.StatusCode}: {errorJson}");
        }

        if (!streaming)
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            return await ResponsesStreamAssembler
                .ReadAsync(reader, interEventTimeout, outputStallTimeout, provider, operation, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsBrokenStream(ex))
        {
            throw new BrokenStreamException(ex);
        }
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

using System.Runtime.CompilerServices;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Reads an SSE body frame by frame with a dead-stream watchdog. Infrastructure for
/// provider packages — every provider that talks SSE goes through this rather than
/// hand-rolling its own read loop.
/// </summary>
/// <remarks>
/// <para>
/// The watchdog is the part worth sharing. Once <c>HttpCompletionOption.ResponseHeadersRead</c>
/// has returned, the resilience pipeline is out of scope: a server that stalls but keeps the
/// socket open (and answers transport keep-alives) would block the read forever. The gap
/// between frames is therefore what gets measured, not the total duration — a long generation
/// is healthy, a silent one is not.
/// </para>
/// <para>
/// Frame parsing is deliberately minimal: SSE <c>event:</c> headers, comments and blank
/// separators are skipped, and only the <c>data:</c> payloads are yielded. Every provider
/// we talk to repeats the event type inside the JSON payload, so nothing is lost.
/// </para>
/// </remarks>
public static class AiSseReader
{
    /// <summary>
    /// Yields each <c>data:</c> payload in order until the stream ends.
    /// </summary>
    /// <param name="reader">Reader over the raw SSE body.</param>
    /// <param name="interEventTimeout">
    /// Maximum gap tolerated between two frames — <c>Ai:Resilience:InterEventTimeout</c>.
    /// Values of zero or less disable the watchdog.
    /// </param>
    /// <param name="provider">Provider name, for diagnostics (e.g. <c>"OpenAI"</c>).</param>
    /// <param name="operation">Calling operation, for diagnostics (e.g. <c>"GenerateAsync"</c>).</param>
    /// <param name="cancellationToken">Caller's token.</param>
    /// <exception cref="TimeoutException">No frame arrived within <paramref name="interEventTimeout"/>.</exception>
    public static async IAsyncEnumerable<string> ReadFramesAsync(
        StreamReader reader,
        TimeSpan interEventTimeout,
        string provider,
        string operation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var watchdogEnabled = interEventTimeout > TimeSpan.Zero;

        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (watchdogEnabled)
            watchdog.CancelAfter(interEventTimeout);

        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(watchdog.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"{provider} {operation} stream produced no event for {interEventTimeout.TotalSeconds:N0}s — "
                    + "server-side stall. Configurable via Ai:Resilience InterEventTimeout.");
            }

            if (line is null) yield break;

            // Refresh on every physical line: `event:` headers, comments and blank frame
            // separators are equally proof that the server is still alive.
            if (watchdogEnabled)
                watchdog.CancelAfter(interEventTimeout);

            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            // "data: {…}" is what every provider sends; "data:{…}" is legal SSE too.
            var payload = line[5..];
            yield return payload.Length > 0 && payload[0] == ' ' ? payload[1..] : payload;
        }
    }
}

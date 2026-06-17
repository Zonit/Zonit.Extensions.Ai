using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// HTTP Messages API transport — the default. Holds the typed, resilience-wrapped
/// <see cref="HttpClient"/> and performs the <c>POST /v1/messages</c> (and SSE stream)
/// that <see cref="AnthropicProvider"/> previously did inline. Authenticates with the
/// configured <c>x-api-key</c>. Behaviour is byte-for-byte the pre-seam provider path.
/// </summary>
internal sealed class AnthropicApiTransport : IAnthropicTransport
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicApiTransport> _logger;

    public AnthropicApiTransport(
        HttpClient httpClient,
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicApiTransport> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        ConfigureHttpClient(options.Value);
    }

    private void ConfigureHttpClient(AnthropicOptions options)
    {
        var baseUrl = options.BaseUrl ?? "https://api.anthropic.com";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        // Always opt-in to three betas (comma-separated; all no-ops when unused,
        // so it is safe to enable globally and avoids per-request header juggling):
        //   • extended-cache-ttl-2025-04-11 — enables cache_control ttl="1h".
        //   • context-1m-2025-08-07 — unlocks the 1M-token context window on
        //     models that support it (Opus / Sonnet 4.6+). Redundant where 1M is
        //     already GA on the first-party API, but required on routes that still
        //     gate it behind the flag; harmless on 200k-only models.
        //   • fast-mode-2026-02-01 — lets a request set speed:"fast" (see IFast).
        //     Inert without the speed field, so safe to send globally — Anthropic's
        //     own fallback example sends this header with standard speed too.
        _httpClient.DefaultRequestHeaders.Add("anthropic-beta", "extended-cache-ttl-2025-04-11,context-1m-2025-08-07,fast-mode-2026-02-01");

        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
        }
    }

    /// <inheritdoc />
    public async Task<AnthropicResponse> SendAsync(
        ILlm llm,
        AnthropicMessagesRequest request,
        string operation,
        CancellationToken cancellationToken)
    {
        var jsonPayload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);
        _logger.LogDebug("Anthropic {Operation} request: {Payload}", operation, jsonPayload);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic {Operation} error: {Status} - {Response}", operation, response.StatusCode, responseJson);
            throw new HttpRequestException(AnthropicProvider.BuildApiErrorMessage(llm, response.StatusCode, responseJson));
        }

        return JsonSerializer.Deserialize(responseJson, AnthropicJsonContext.Default.AnthropicResponse)!;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        ILlm llm,
        AnthropicMessagesRequest request,
        string operation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var jsonPayload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/messages") { Content = content };
        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var emittedAny = false;
        string? lastStopReason = null;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];
            var chunk = JsonSerializer.Deserialize(data, AnthropicJsonContext.Default.StreamEvent);

            if (chunk?.Type == "content_block_delta" && chunk.Delta?.Text != null)
            {
                emittedAny = true;
                yield return chunk.Delta.Text;
            }
            else if (chunk?.Type == "message_delta" && chunk.Delta?.StopReason is { } sr)
            {
                lastStopReason = sr;
                LogTerminalStreamStopReason(operation, sr);
            }
        }

        // A stream that ended without ever emitting text is the same server-side
        // empty/data-loss fault as a non-streaming empty response — surface it as the
        // same typed exception rather than completing as a silent empty sequence.
        if (!emittedAny && !cancellationToken.IsCancellationRequested)
            throw AnthropicProvider.BuildEmptyResponseError(_logger, operation, llm, lastStopReason,
                response.Headers.TryGetValues("request-id", out var ids) ? ids.FirstOrDefault() : null);
    }

    /// <summary>
    /// Diagnostic-only logging for non-agent streaming paths. We do not
    /// auto-retry single-shot streams on <c>pause_turn</c> (that belongs to
    /// the agent loop, which buffers full assistant content and re-issues
    /// transparently) but we still want a clear log line so a silently
    /// truncated stream is never invisible to the operator.
    /// </summary>
    private void LogTerminalStreamStopReason(string operation, string stopReason)
    {
        switch (stopReason)
        {
            case "end_turn":
            case "stop_sequence":
                // Normal terminations — no log needed.
                break;
            case "max_tokens":
                _logger.LogWarning(
                    "Anthropic {Operation} stream terminated with stop_reason=max_tokens — the response was truncated. " +
                    "Increase MaxOutputTokens on the model or lower thinking effort.",
                    operation);
                break;
            case "pause_turn":
                _logger.LogWarning(
                    "Anthropic {Operation} stream terminated with stop_reason=pause_turn — the server-side sampling " +
                    "loop hit its iteration limit (server tools). Single-shot streams do not auto-resume; switch to " +
                    "the agent path (IAgentLlm) for transparent pause_turn continuations.",
                    operation);
                break;
            case "refusal":
                _logger.LogWarning(
                    "Anthropic {Operation} stream terminated with stop_reason=refusal — the model declined to continue.",
                    operation);
                break;
            default:
                _logger.LogDebug(
                    "Anthropic {Operation} stream terminated with stop_reason={StopReason}.",
                    operation, stopReason);
                break;
        }
    }
}

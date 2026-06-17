namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Transport seam for the Anthropic provider. <see cref="AnthropicProvider"/> builds
/// and parses the canonical <see cref="AnthropicMessagesRequest"/> /
/// <see cref="AnthropicResponse"/> transport-agnostically; the transport is the only
/// thing that moves bytes. Two implementations exist:
/// <list type="bullet">
///   <item><description><see cref="AnthropicApiTransport"/> — the HTTP Messages API
///   (<c>x-api-key</c>), the default.</description></item>
///   <item><description><see cref="AnthropicCliTransport"/> — the local Claude Code
///   CLI (<c>claude -p</c>, the Claude Agent SDK) as a subprocess, so requests use the
///   user's <c>claude login</c> session instead of an API key.</description></item>
/// </list>
/// The active transport is chosen by <see cref="AnthropicOptions.Transport"/> at DI time.
/// </summary>
internal interface IAnthropicTransport
{
    /// <summary>
    /// Non-streaming send (<c>GenerateAsync</c> / <c>ChatAsync</c>): returns the same
    /// <see cref="AnthropicResponse"/> shape the API path produces, regardless of
    /// transport. <paramref name="operation"/> is the caller name used in diagnostics.
    /// </summary>
    Task<AnthropicResponse> SendAsync(
        ILlm llm,
        AnthropicMessagesRequest request,
        string operation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streaming send (<c>StreamAsync</c> / <c>ChatStreamAsync</c>): yields text
    /// deltas. A stream that completes without emitting any text is a server-side
    /// empty/data-loss fault and must surface as the same
    /// <see cref="AiEmptyResponseException"/> the non-streaming path raises — the
    /// transport owns that final guard since only it sees the terminal stop reason.
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(
        ILlm llm,
        AnthropicMessagesRequest request,
        string operation,
        CancellationToken cancellationToken);
}

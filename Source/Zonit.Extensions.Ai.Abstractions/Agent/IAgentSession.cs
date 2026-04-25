namespace Zonit.Extensions.Ai;

/// <summary>
/// A stateful agent session bound to a specific provider + model for the duration
/// of a single <c>GenerateAsync</c> call. Holds the provider-specific conversation
/// history (messages, tool_use blocks, previous_response_id, etc.).
/// </summary>
/// <remarks>
/// The runner drives the session in a loop: the first call passes <c>null</c> as
/// <c>toolResults</c>; subsequent calls supply the results produced for the
/// tool calls returned by the previous turn.
/// </remarks>
public interface IAgentSession : IAsyncDisposable
{
    /// <summary>
    /// Executes a single model round-trip. Pass <c>null</c> for the initial call,
    /// or the results of the previously-returned tool calls for continuations.
    /// </summary>
    /// <param name="toolResults">
    /// Results of the tools executed after the previous turn. <c>null</c> on the
    /// very first invocation. Must cover every <see cref="PendingToolCall"/>
    /// returned previously (possibly with error payloads).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resulting turn — either more tool calls or a final answer.</returns>
    Task<AgentTurn> RunTurnAsync(
        IReadOnlyList<ToolResult>? toolResults,
        CancellationToken cancellationToken);
}

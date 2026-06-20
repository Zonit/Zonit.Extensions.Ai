namespace Zonit.Extensions.Ai;

/// <summary>
/// Read-only facts about the current run's conversation, seeded by the framework into the run's
/// <see cref="IRunContext"/> so tools and sub-agents can read them the same way they read your own
/// context — <c>context.Get&lt;ConversationInfo&gt;()</c>. Unlike values you pass via
/// <c>WithContext(...)</c>, this one is supplied by the library, not the caller.
/// </summary>
/// <remarks>
/// <para>
/// The canonical use is gating a sub-agent on whether the conversation has started yet: an "opener"
/// that greets the user is shown only when the history is empty.
/// <code>
/// public override bool IsAvailable(IRunContext context)
///     =&gt; context.Get&lt;ConversationInfo&gt;()?.IsEmpty == true;
/// </code>
/// </para>
/// <para>
/// The count reflects the conversation as it stood when the run began (the forwarded chat history),
/// so it is meaningful in <c>IsAvailable</c> — evaluated once before the loop — as well as in a
/// tool's <c>ExecuteAsync</c>. A plain agent run (<c>ai.Agent(...)</c>) carries no conversation, so
/// <see cref="MessageCount"/> is <c>0</c>. Each sub-agent run is seeded with its <i>own</i>
/// <see cref="ConversationInfo"/>, reflecting the history forwarded to that sub-agent.
/// </para>
/// </remarks>
public sealed record ConversationInfo
{
    /// <summary>
    /// Number of messages in the conversation forwarded into this run. <c>0</c> when the run starts
    /// from an empty conversation (or a plain <c>ai.Agent(...)</c> run with no history).
    /// </summary>
    public required int MessageCount { get; init; }

    /// <summary>Convenience: <c>true</c> when <see cref="MessageCount"/> is <c>0</c> (a fresh conversation).</summary>
    public bool IsEmpty => MessageCount == 0;
}

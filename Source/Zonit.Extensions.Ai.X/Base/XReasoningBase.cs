namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Base class for X/Grok reasoning models.
/// </summary>
public abstract class XReasoningBase : XChatBase, IReasoningLlm
{
    /// <inheritdoc />
    public virtual ReasoningEffort? Reason { get; init; }

    /// <inheritdoc />
    public virtual ReasoningSummary? ReasonSummary { get; init; }

    /// <inheritdoc />
    public virtual Verbosity? OutputVerbosity { get; init; }
}

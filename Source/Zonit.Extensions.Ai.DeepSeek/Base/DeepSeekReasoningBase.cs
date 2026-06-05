namespace Zonit.Extensions.Ai.DeepSeek;

/// <summary>
/// Base class for DeepSeek reasoning models (R1 series).
/// </summary>
public abstract class DeepSeekReasoningBase : DeepSeekBase, IReasoningLlm
{
    /// <inheritdoc />
    public virtual ReasoningEffort? Reason { get; init; }

    /// <inheritdoc />
    public virtual ReasoningSummary? ReasonSummary { get; init; }

    /// <inheritdoc />
    public virtual Verbosity? OutputVerbosity { get; init; }
}

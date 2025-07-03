namespace Zonit.Extensions.Ai.Llm;

public abstract class OpenAiReasoningBase : OpenAiBase
{
    public virtual decimal? PriceCachedInput { get; } = null;

    public virtual ReasonType? Reason { get; init; }
    public virtual ReasonSummaryType? ReasonSummary { get; init; }

    public enum ReasonType
    {
        Low,
        Medium,
        High
    }
    public enum ReasonSummaryType
    {
        None,
        Auto,
        Detailed,
    }
}

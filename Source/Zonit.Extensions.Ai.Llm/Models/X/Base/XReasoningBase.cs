namespace Zonit.Extensions.Ai.Llm.X;

public abstract class XReasoningBase : XChatBase, ITextLlmBase
{
    public virtual ReasonType? Reason { get; init; }

    public enum ReasonType
    {
        Low,
        High
    }
}
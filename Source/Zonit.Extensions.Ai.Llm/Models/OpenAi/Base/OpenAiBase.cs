namespace Zonit.Extensions.Ai.Llm;

public abstract class OpenAiBase : LlmBase
{
    public bool StoreLogs { get; set; } = false;
}
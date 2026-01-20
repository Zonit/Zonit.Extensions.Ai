namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Base class for all OpenAI models.
/// </summary>
public abstract class OpenAiBase : LlmBase
{
    /// <summary>
    /// Whether to store logs for this model usage.
    /// </summary>
    public bool StoreLogs { get; set; } = false;
}

namespace Zonit.Extensions.Ai;

/// <summary>
/// Capability marker: the LLM supports the agent tool-call loop
/// (can request function calls and accept tool results across iterations).
/// </summary>
/// <remarks>
/// Only models that support function / tool calling should implement this
/// interface. It enables compile-time safety for the agent overloads of
/// <see cref="IAiProvider"/> — you cannot accidentally invoke the agent
/// loop on an embedding, audio or image-only model.
/// <para>
/// Typical implementers: <c>GPT5</c>, <c>GPT41</c>, <c>Claude45Sonnet</c>,
/// <c>Gemini25Pro</c>, <c>Grok4</c>, etc.
/// </para>
/// </remarks>
public interface IAgentLlm : ILlm
{
    /// <summary>
    /// Default maximum number of agent iterations (one iteration = one
    /// round-trip "model call → optional tool executions").
    /// </summary>
    /// <remarks>
    /// Large but safe ceiling (default 100). Agents legitimately perform
    /// many tool calls. Can be overridden by <see cref="AgentOptions.MaxIterations"/>
    /// or by the global <c>Ai:Agent:MaxIterations</c> configuration.
    /// </remarks>
    int DefaultMaxIterations => 100;
}

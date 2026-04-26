using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Provider-side adapter that knows how to drive a single provider's API in
/// agent mode (tool calling, tool-result feedback, structured-output parsing).
/// </summary>
/// <remarks>
/// One implementation per provider package (<c>OpenAiAgentAdapter</c>,
/// <c>AnthropicAgentAdapter</c>, ...). The core <c>AgentRunner</c> picks the
/// adapter by calling <see cref="SupportsAgent"/>.
/// <para>
/// Adapters do <b>not</b> execute tools and do <b>not</b> run the loop — that's
/// the runner's job. The adapter is responsible only for:
/// <list type="bullet">
///   <item><description>translating <see cref="AgentSessionContext"/> into a provider request,</description></item>
///   <item><description>parsing the provider response into <see cref="PendingToolCall"/>s or a final text,</description></item>
///   <item><description>appending tool results for subsequent iterations,</description></item>
///   <item><description>maintaining any provider-specific conversation state (server-side <c>previous_response_id</c>, client-side message history, etc.).</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IAgentProviderAdapter
{
    /// <summary>
    /// Whether this adapter supports the given model in agent mode.
    /// </summary>
    bool SupportsAgent(ILlm llm);

    /// <summary>
    /// Opens a new agent session. The caller disposes the returned instance
    /// after <c>GenerateAsync</c> completes (or the cancellation token fires).
    /// </summary>
    [RequiresUnreferencedCode("Agent session may invoke JsonSchemaGenerator.Generate via the runner.")]
    [RequiresDynamicCode("Agent session may invoke JsonSchemaGenerator.Generate via the runner.")]
    IAgentSession BeginSession(AgentSessionContext context);
}

namespace Zonit.Extensions.Ai;

/// <summary>
/// Input bag for <see cref="IAgentProviderAdapter.BeginSession"/>. Carries the
/// model, initial prompt, the exposed tool set and (optionally) the structured-output type.
/// </summary>
public sealed class AgentSessionContext
{
    /// <summary>
    /// The agent-capable LLM to drive.
    /// </summary>
    public required IAgentLlm Llm { get; init; }

    /// <summary>
    /// Initial user prompt (may carry a system message and files).
    /// </summary>
    public required IPrompt Prompt { get; init; }

    /// <summary>
    /// Expected structured-output type, or <c>null</c> when the model should produce
    /// free-form text. Set from the caller's <c>TResponse</c> generic argument.
    /// </summary>
    public Type? ResponseType { get; init; }

    /// <summary>
    /// All tools (custom + MCP-exposed) the model may call in this session.
    /// </summary>
    public required IReadOnlyList<ITool> Tools { get; init; }

    /// <summary>
    /// Optional pre-existing chat history (User/Assistant/Tool messages) seeded into
    /// the agent session before the first turn. Provided by <c>IAiProvider.ChatAsync</c>
    /// when the caller starts the agent run mid-conversation. <c>null</c> when the
    /// agent starts from scratch (classic <c>GenerateAsync</c> flow).
    /// </summary>
    public IReadOnlyList<ChatMessage>? InitialChat { get; init; }
}

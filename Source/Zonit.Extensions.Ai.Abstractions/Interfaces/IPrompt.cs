using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Non-generic base for AI prompts. Useful when the response type is not known
/// at compile time (e.g. the agent runner accepts prompts of any <c>TResponse</c>
/// by working against this base).
/// </summary>
/// <remarks>
/// Every <see cref="IPrompt{TResponse}"/> implements this interface automatically —
/// both interfaces share the same member set.
/// </remarks>
public interface IPrompt
{
    /// <summary>
    /// The prompt text sent to the AI.
    /// </summary>
    /// <remarks>
    /// Semantic role depends on the call site:
    /// <list type="bullet">
    ///   <item><description><c>GenerateAsync(llm, prompt)</c> single-shot — used as the <b>user</b> message.</description></item>
    ///   <item><description><c>ChatAsync(llm, prompt, chat)</c> / agent runs — used as the <b>system</b> instruction; the conversation lives in <c>chat[]</c>.</description></item>
    /// </list>
    /// For typed prompts inheriting <c>PromptBase&lt;T&gt;</c>, this is the rendered output of the
    /// raw <c>Prompt</c> template (rendering happens in the application layer, not in abstractions).
    /// </remarks>
    string Text { get; }

    /// <summary>
    /// Files attached to this prompt (images, documents, etc.).
    /// Uses the Asset value object from Zonit.Extensions.
    /// </summary>
    IReadOnlyList<Asset>? Files { get; }
}

/// <summary>
/// Base interface for AI prompts.
/// Prompt contains user input - text, files, system message.
/// The generic parameter defines what type is returned.
/// </summary>
/// <typeparam name="TResponse">The expected response type - strongly typed!</typeparam>
public interface IPrompt<TResponse> : IPrompt
{
}

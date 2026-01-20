namespace Zonit.Extensions.Ai;

/// <summary>
/// Base interface for AI prompts.
/// Prompt contains user input - text, files, system message.
/// The generic parameter defines what type is returned.
/// </summary>
/// <typeparam name="TResponse">The expected response type - strongly typed!</typeparam>
public interface IPrompt<TResponse>
{
    /// <summary>
    /// System message/instruction for the AI.
    /// </summary>
    string? System { get; }
    
    /// <summary>
    /// The main prompt text sent to the AI.
    /// </summary>
    string Text { get; }
    
    /// <summary>
    /// Files attached to this prompt (images, documents, etc.).
    /// </summary>
    IReadOnlyList<AiFile>? Files { get; }
}

namespace Zonit.Extensions.Ai;

/// <summary>
/// Renders an <see cref="IPrompt"/> into its final text form.
/// </summary>
/// <remarks>
/// For <see cref="PromptBase{TResponse}"/> subclasses, the renderer uses the
/// raw <c>Prompt</c> template plus the prompt's public properties (mapped to
/// snake_case Scriban variables). For prompts whose <c>Text</c> is already
/// rendered (e.g. <c>SimplePrompt</c>), the renderer returns it as-is.
/// </remarks>
public interface IPromptRenderer
{
    /// <summary>Renders the prompt to its final text form.</summary>
    string Render(IPrompt prompt);
}

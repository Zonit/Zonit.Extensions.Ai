using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai;

public interface IPromptBase
{
    /// <summary>
    /// Narzędzia dostępne dla AI (może być wiele).
    /// </summary>
    public IReadOnlyList<ITool>? Tools { get; }

    /// <summary>
    /// Narzędzie wymuszone przez użytkownika (jedno lub null).
    /// </summary>
    public ToolsType? ToolChoice { get; }

    /// <summary>
    /// Gets or sets the username associated with the user account.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Lista plików dołączonych do promptu.
    /// </summary>
    public IReadOnlyList<IFile>? Files { get; }

    public string Prompt { get; }
}

public interface IPromptBase<TResponse> : IPromptBase
{
    /// <summary>
    /// Gets the model type associated with the prompt.
    /// </summary>
    public TResponse? ModelType { get; }
}

public abstract class PromptBase<TResponse> : IPromptBase<TResponse> where TResponse : class
{
    public virtual IReadOnlyList<ITool>? Tools { get; }
    public virtual ToolsType? ToolChoice { get; }
    public virtual string? UserName { get; set; }
    public virtual IReadOnlyList<IFile>? Files { get; set; }
    public virtual TResponse? ModelType { get; }
    public abstract string Prompt { get; }
}
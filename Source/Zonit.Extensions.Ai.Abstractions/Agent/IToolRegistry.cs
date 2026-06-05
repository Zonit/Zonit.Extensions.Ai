namespace Zonit.Extensions.Ai;

/// <summary>
/// Registry of custom agent tools discovered in the DI container.
/// Populated by <c>AddAiTool&lt;T&gt;()</c> and the source-generated
/// <c>AddAiTools()</c> extension.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Returns a snapshot of all currently registered tools.
    /// </summary>
    IReadOnlyList<ITool> GetAll();

    /// <summary>
    /// Attempts to resolve a tool by its <see cref="ITool.Name"/>.
    /// </summary>
    ITool? Get(string name);
}

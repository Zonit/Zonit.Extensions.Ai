using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Bazowy interfejs dla wszystkich narzędzi AI.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Typ narzędzia (web_search, file_search, code_interpreter, etc.).
    /// </summary>
    ToolsType Type { get; }
}
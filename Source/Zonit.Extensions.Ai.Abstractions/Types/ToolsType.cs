namespace Zonit.Extensions.Ai;

/// <summary>
/// Tools supported by AI models.
/// </summary>
[Flags]
public enum ToolsType
{
    None = 0,
    WebSearch = 1 << 0,
    FileSearch = 1 << 1,
    ImageGeneration = 1 << 2,
    CodeInterpreter = 1 << 3,
    MCP = 1 << 4,
    CodeExecution = 1 << 5,
    XSearch = 1 << 6,
    DocumentSearch = 1 << 7,
    CollectionsSearch = 1 << 8,
}

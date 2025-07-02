namespace Zonit.Extensions.Ai.Llm;

[Flags]
public enum ToolsType
{
    None = 0,
    WebSearch = 1 << 0,
    FileSearch = 1 << 1,
    ImageGeneration = 1 << 2,
    CodeInterpreter = 1 << 3,
    MCP = 1 << 4,
}
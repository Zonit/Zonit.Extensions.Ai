using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Abstractions;

public class WebSearchTool : ITool
{
    public ToolsType Type => ToolsType.WebSearch;

    public string? Country { get; init; }
    public string? Region { get; init; }
    public string? City { get; init; }
    public string? TimeZone { get; init; }

    public ContextSizeType ContextSize { get; set; } = ContextSizeType.Medium;

    public enum ContextSizeType
    {
        Low,
        Medium,
        High
    }
}
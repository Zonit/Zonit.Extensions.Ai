namespace Zonit.Extensions.Ai;

/// <summary>
/// Web search tool with location and context configuration.
/// </summary>
public class WebSearchTool : IToolBase
{
    /// <summary>
    /// Country code for search results.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// Region code for search results.
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// City for search results.
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// TimeZone for search results.
    /// </summary>
    public string? TimeZone { get; init; }

    /// <summary>
    /// Context size for search results.
    /// </summary>
    public ContextSizeType ContextSize { get; set; } = ContextSizeType.Medium;

    public enum ContextSizeType
    {
        Low,
        Medium,
        High
    }
}

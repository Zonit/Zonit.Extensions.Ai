using System.ComponentModel;

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Search source type for Grok models.
/// </summary>
public enum SourceType
{
    /// <summary>
    /// General web search results.
    /// </summary>
    [Description("web")]
    Web,

    /// <summary>
    /// X (Twitter) posts and content.
    /// </summary>
    [Description("x")]
    X,

    /// <summary>
    /// News articles and reports.
    /// </summary>
    [Description("news")]
    News,

    /// <summary>
    /// RSS feed content.
    /// </summary>
    [Description("rss")]
    Rss
}

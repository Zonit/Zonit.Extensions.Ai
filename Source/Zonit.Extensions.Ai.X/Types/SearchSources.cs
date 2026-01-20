namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Base interface for all search sources.
/// </summary>
public interface ISearchSource
{
    SourceType Type { get; }
}

/// <summary>
/// Web search source configuration.
/// </summary>
public class WebSearchSource : ISearchSource
{
    public SourceType Type => SourceType.Web;

    /// <summary>
    /// Country code (ISO alpha-2) to focus search results.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// List of websites to exclude from search (max 5).
    /// </summary>
    public string[]? ExcludedWebsites { get; init; }

    /// <summary>
    /// List of websites to search exclusively (max 5). Cannot be used with ExcludedWebsites.
    /// </summary>
    public string[]? AllowedWebsites { get; init; }

    /// <summary>
    /// Enable or disable safe search (default: true).
    /// </summary>
    public bool SafeSearch { get; init; } = true;
}

/// <summary>
/// X (Twitter) search source configuration.
/// </summary>
public class XSearchSource : ISearchSource
{
    public SourceType Type => SourceType.X;

    /// <summary>
    /// List of X handles to include in search (max 10).
    /// </summary>
    public string[]? IncludedXHandles { get; init; }

    /// <summary>
    /// List of X handles to exclude from search (max 10).
    /// </summary>
    public string[]? ExcludedXHandles { get; init; }

    /// <summary>
    /// Minimum number of favorites required for posts.
    /// </summary>
    public int? PostFavoriteCount { get; init; }

    /// <summary>
    /// Minimum number of views required for posts.
    /// </summary>
    public int? PostViewCount { get; init; }
}

/// <summary>
/// News search source configuration.
/// </summary>
public class NewsSearchSource : ISearchSource
{
    public SourceType Type => SourceType.News;

    /// <summary>
    /// Country code (ISO alpha-2) to focus search results.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// List of news websites to exclude from search (max 5).
    /// </summary>
    public string[]? ExcludedWebsites { get; init; }

    /// <summary>
    /// Enable or disable safe search (default: true).
    /// </summary>
    public bool SafeSearch { get; init; } = true;
}

/// <summary>
/// RSS feed search source configuration.
/// </summary>
public class RssSearchSource : ISearchSource
{
    public SourceType Type => SourceType.Rss;

    /// <summary>
    /// RSS feed URLs to search (currently supports 1 link).
    /// </summary>
    public string[]? Links { get; init; }
}

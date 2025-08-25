namespace Zonit.Extensions.Ai.Llm.X;

public abstract class XChatBase : XBase, ITextLlmBase
{
    public abstract decimal PriceCachedInput { get; }

    public virtual Search WebSearch { get; init; } = new Search();
}

public class Search
{
    /// <summary>
    /// Controls how search is used. Auto determines when to use search based on the query.
    /// </summary>
    public virtual ModeType Mode { get; init; } = ModeType.Auto;

    /// <summary>
    /// The live search endpoint supports returning citations to the data sources used in the response in the form of a list of URLs. This field defaults to true
    /// </summary>
    public virtual bool Citations { get; init; } = true;

    /// <summary>
    /// Filter results to only include content published after this date
    /// </summary>
    public virtual DateTime? FromDate { get; init; } = null;

    /// <summary>
    /// Filter results to only include content published before this date
    /// </summary>
    public virtual DateTime? ToDate { get; init; } = null;

    /// <summary>
    /// Maximum number of search results to return (default: 20)
    /// </summary>
    public virtual int MaxResults { get; init; } = 20;

    /// <summary>
    /// Specify which sources to search with their specific configurations.
    /// </summary>
    public virtual ISearchSource[]? Sources { get; init; } = null;

    /// <summary>
    /// Language preference for search results (ISO 639-1 code, e.g., "en", "pl")
    /// </summary>
    public virtual string? Language { get; init; } = null;

    /// <summary>
    /// Geographic region to focus search results (ISO 3166-1 alpha-2 code, e.g., "US", "PL")
    /// </summary>
    public virtual string? Region { get; init; } = null;
}

/// <summary>
/// Base interface for all search sources
/// </summary>
public interface ISearchSource
{
    SourceType Type { get; }
}

/// <summary>
/// Web search source configuration
/// </summary>
public class WebSearchSource : ISearchSource
{
    public SourceType Type => SourceType.Web;
    
    /// <summary>
    /// Country code (ISO alpha-2) to focus search results
    /// </summary>
    public string? Country { get; init; }
    
    /// <summary>
    /// List of websites to exclude from search (max 5)
    /// </summary>
    public string[]? ExcludedWebsites { get; init; }
    
    /// <summary>
    /// List of websites to search exclusively (max 5). Cannot be used with ExcludedWebsites.
    /// </summary>
    public string[]? AllowedWebsites { get; init; }
    
    /// <summary>
    /// Enable or disable safe search (default: true)
    /// </summary>
    public bool SafeSearch { get; init; } = true;
}

/// <summary>
/// X (Twitter) search source configuration
/// </summary>
public class XSearchSource : ISearchSource
{
    public SourceType Type => SourceType.X;
    
    /// <summary>
    /// List of X handles to include in search (max 10). Cannot be used with ExcludedXHandles.
    /// </summary>
    public string[]? IncludedXHandles { get; init; }
    
    /// <summary>
    /// List of X handles to exclude from search (max 10). Cannot be used with IncludedXHandles.
    /// </summary>
    public string[]? ExcludedXHandles { get; init; }
    
    /// <summary>
    /// Minimum number of favorites required for posts to be considered
    /// </summary>
    public int? PostFavoriteCount { get; init; }
    
    /// <summary>
    /// Minimum number of views required for posts to be considered
    /// </summary>
    public int? PostViewCount { get; init; }
}

/// <summary>
/// News search source configuration
/// </summary>
public class NewsSearchSource : ISearchSource
{
    public SourceType Type => SourceType.News;
    
    /// <summary>
    /// Country code (ISO alpha-2) to focus search results
    /// </summary>
    public string? Country { get; init; }
    
    /// <summary>
    /// List of news websites to exclude from search (max 5)
    /// </summary>
    public string[]? ExcludedWebsites { get; init; }
    
    /// <summary>
    /// Enable or disable safe search (default: true)
    /// </summary>
    public bool SafeSearch { get; init; } = true;
}

/// <summary>
/// RSS feed search source configuration
/// </summary>
public class RssSearchSource : ISearchSource
{
    public SourceType Type => SourceType.Rss;
    
    /// <summary>
    /// RSS feed URLs to search (currently supports 1 link)
    /// </summary>
    public string[]? Links { get; init; }
}
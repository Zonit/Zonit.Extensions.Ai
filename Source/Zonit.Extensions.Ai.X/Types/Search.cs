namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Search configuration for Grok models using Agent Tools API.
/// Supports web search and X (Twitter) search with advanced filtering.
/// </summary>
public class Search
{
    /// <summary>
    /// Controls how search is used. Auto determines when to use search based on the query.
    /// </summary>
    public virtual ModeType Mode { get; init; } = ModeType.Auto;

    /// <summary>
    /// Returns citations to data sources as a list of URLs.
    /// Note: Citations are automatically returned by the Agent Tools API regardless of this setting.
    /// </summary>
    [Obsolete("Citations are automatically provided by the X Agent Tools API. This property is kept for backward compatibility but has no effect.")]
    public virtual bool Citations { get; init; } = true;

    /// <summary>
    /// Filter results to only include content published after this date.
    /// </summary>
    public virtual DateTime? FromDate { get; init; } = null;

    /// <summary>
    /// Filter results to only include content published before this date.
    /// </summary>
    public virtual DateTime? ToDate { get; init; } = null;

    /// <summary>
    /// Maximum number of search results to return (default: 20).
    /// </summary>
    public virtual int MaxResults { get; init; } = 20;

    /// <summary>
    /// Specify which sources to search with their specific configurations.
    /// </summary>
    public virtual ISearchSource[]? Sources { get; init; } = null;

    /// <summary>
    /// Language preference for search results (ISO 639-1 code, e.g., "en", "pl").
    /// Note: Not supported by Agent Tools API. Use Country property in WebSearchSource instead.
    /// </summary>
    [Obsolete("Not supported by the X Agent Tools API. Use the Country property in WebSearchSource for region-specific results.")]
    public virtual string? Language { get; init; } = null;

    /// <summary>
    /// Geographic region to focus search results (ISO 3166-1 alpha-2 code, e.g., "US", "PL").
    /// Note: Not supported by Agent Tools API. Use Country property in WebSearchSource instead.
    /// </summary>
    [Obsolete("Not supported by the X Agent Tools API. Use the Country property in WebSearchSource for region-specific results.")]
    public virtual string? Region { get; init; } = null;
}

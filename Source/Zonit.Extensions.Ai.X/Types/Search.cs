namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Search configuration for Grok models.
/// </summary>
public class Search
{
    /// <summary>
    /// Controls how search is used. Auto determines when to use search based on the query.
    /// </summary>
    public virtual ModeType Mode { get; init; } = ModeType.Auto;

    /// <summary>
    /// Returns citations to data sources as a list of URLs. Defaults to true.
    /// </summary>
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
    /// </summary>
    public virtual string? Language { get; init; } = null;

    /// <summary>
    /// Geographic region to focus search results (ISO 3166-1 alpha-2 code, e.g., "US", "PL").
    /// </summary>
    public virtual string? Region { get; init; } = null;
}

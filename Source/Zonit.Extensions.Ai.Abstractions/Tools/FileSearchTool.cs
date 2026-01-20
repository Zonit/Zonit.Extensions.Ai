namespace Zonit.Extensions.Ai;

/// <summary>
/// File search tool for OpenAI models that enables searching through uploaded files using vector stores.
/// </summary>
/// <example>
/// var model = new GPT41
/// {
///     Tools = new IToolBase[]
///     {
///         new FileSearchTool
///         {
///             VectorId = "vs_abc123456789",
///             MaxNumResults = 10,
///             RankingOptions = new FileSearchTool.RankingOptionsType
///             {
///                 Ranker = "default_2024_08_21",
///                 ScoreThreshold = 0.7
///             }
///         }
///     }
/// };
/// </example>
public class FileSearchTool : IToolBase
{
    /// <summary>
    /// Vector store ID for file search. Should be the ID of a vector store you've created.
    /// Example: "vs_abc123456789"
    /// </summary>
    public virtual string? VectorId { get; init; }

    /// <summary>
    /// Maximum number of results to return from the file search.
    /// Default is 20, maximum is 50.
    /// </summary>
    public virtual int? MaxNumResults { get; init; }

    /// <summary>
    /// Ranking options for the file search results.
    /// </summary>
    public virtual RankingOptionsType? RankingOptions { get; init; }

    /// <summary>
    /// Metadata filters for the file search.
    /// Example: new { type = "eq", key = "document_type", value = "manual" }
    /// </summary>
    public virtual object? Filters { get; init; }

    public class RankingOptionsType
    {
        /// <summary>
        /// The ranker to use. Can be 'default_2024_08_21' or 'auto' (default).
        /// </summary>
        public string? Ranker { get; init; }

        /// <summary>
        /// Score threshold (0.0 - 1.0). Only chunks with a score above this will be returned.
        /// </summary>
        public double? ScoreThreshold { get; init; }
    }
}

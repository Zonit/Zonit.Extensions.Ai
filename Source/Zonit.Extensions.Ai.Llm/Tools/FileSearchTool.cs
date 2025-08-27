namespace Zonit.Extensions.Ai.Llm;

/// <summary>
/// File search tool for OpenAI models that enables searching through uploaded files using vector stores.
/// 
/// Example usage:
/// 
/// // Create a model with FileSearchTool configured
/// var model = new GPT41
/// {
///     Tools = new IToolBase[]
///     {
///         new FileSearchTool
///         {
///             VectorId = "vs_abc123456789", // Your vector store ID from OpenAI
///             MaxNumResults = 10,
///             RankingOptions = new FileSearchTool.RankingOptionsType
///             {
///                 Ranker = "default_2024_08_21",
///                 ScoreThreshold = 0.7
///             },
///             Filters = new // Metadata filtering
///             {
///                 type = "eq",
///                 key = "document_type", 
///                 value = "manual"
///             }
///         }
///     }
/// };
/// 
/// // Use with any prompt
/// var result = await aiClient.ResponseAsync(model, prompt);
/// </summary>
public class FileSearchTool : IToolBase
{
    /// <summary>
    /// Vector store ID for file search. If provided, uses the specified vector store.
    /// This should be the ID of a vector store you've created and uploaded files to via OpenAI API.
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
    /// Use this to filter search results based on file metadata.
    /// Example: new { type = "eq", key = "document_type", value = "manual" }
    /// </summary>
    public virtual object? Filters { get; init; }

    public class RankingOptionsType
    {
        /// <summary>
        /// The ranker to use for the file search. Can be 'default_2024_08_21' or 'auto' (default).
        /// </summary>
        public string? Ranker { get; init; }

        /// <summary>
        /// The score threshold for the file search. Only chunks with a score above this threshold will be returned.
        /// Value should be between 0.0 and 1.0.
        /// </summary>
        public double? ScoreThreshold { get; init; }
    }
}

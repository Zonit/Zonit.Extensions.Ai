using System.Text.Json;

namespace Zonit.Extensions.Ai.OpenAi.Tools;

/// <summary>
/// OpenAI Responses API <c>file_search</c> server tool. Backed by an OpenAI
/// vector store (<see cref="VectorId"/>); the model receives the top
/// <see cref="MaxNumResults"/> chunks ranked by the configured
/// <see cref="RankingOptions"/> ranker. Optional <see cref="Filters"/> are
/// forwarded verbatim as the file_search <c>filters</c> field — supply a
/// <see cref="JsonElement"/> (single comparison filter object, or an array for a
/// compound filter) matching the JSON shape OpenAI expects.
/// </summary>
public class FileSearchTool : IOpenAiTool
{
    /// <summary>Vector store ID (<c>vs_...</c>) the search is scoped to.</summary>
    public string? VectorId { get; init; }

    /// <summary>Maximum chunks returned to the model (1–50). Null lets OpenAI pick the default.</summary>
    public int? MaxNumResults { get; init; }

    /// <summary>Ranker configuration. Null falls back to the default ranker.</summary>
    public RankingOptionsType? RankingOptions { get; init; }

    /// <summary>
    /// Pass-through metadata filter, forwarded verbatim as the <c>filters</c> field on
    /// the tool descriptor. Supply a pre-built <see cref="JsonElement"/> (e.g. from
    /// <c>JsonSerializer.SerializeToElement(...)</c> with your own AOT-safe context, or
    /// <c>JsonDocument.Parse(...)</c>) so the provider needs no reflection.
    /// </summary>
    public JsonElement? Filters { get; init; }

    public sealed class RankingOptionsType
    {
        /// <summary>Ranker name (<c>auto</c>, <c>default_2024_08_21</c>, …).</summary>
        public string? Ranker { get; init; }
        /// <summary>Score threshold (0..1) below which chunks are dropped.</summary>
        public double? ScoreThreshold { get; init; }
    }
}

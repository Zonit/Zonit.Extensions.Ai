namespace Zonit.Extensions.Ai.X.Tools;

/// <summary>
/// xAI-only server tool that searches the X (Twitter) timeline. Wires to the
/// <c>x_search</c> tool block on the xAI Responses API. Use this when you
/// specifically need posts from X — for general web search prefer
/// <see cref="WebSearchTool"/>.
/// </summary>
public class XSearchTool : IXTool
{
    /// <summary>Restrict results to posts from these X handles (without the leading @).</summary>
    public IReadOnlyList<string>? IncludedHandles { get; init; }

    /// <summary>Exclude posts from these X handles.</summary>
    public IReadOnlyList<string>? ExcludedHandles { get; init; }

    /// <summary>Earliest post date considered (inclusive). Maps to the <c>from_date</c> field.</summary>
    public DateOnly? FromDate { get; init; }

    /// <summary>Latest post date considered (inclusive). Maps to the <c>to_date</c> field.</summary>
    public DateOnly? ToDate { get; init; }
}

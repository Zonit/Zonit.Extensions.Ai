namespace Zonit.Extensions.Ai.X.Tools;

/// <summary>
/// xAI Responses API <c>web_search</c> server tool — general-purpose internet
/// search. Use <see cref="XSearchTool"/> instead when you specifically want
/// X (Twitter) timeline results.
/// </summary>
/// <remarks>
/// The legacy Live Search on Chat Completions is deprecated; this tool is
/// only consumable on the <c>/v1/responses</c> endpoint.
/// </remarks>
public class WebSearchTool : IXTool
{
    /// <summary>Optional allow-list of domains the search may return results from.</summary>
    public IReadOnlyList<string>? AllowedDomains { get; init; }

    /// <summary>Optional block-list of domains excluded from results.</summary>
    public IReadOnlyList<string>? ExcludedDomains { get; init; }

    /// <summary>
    /// Lets Grok download and reason over images attached to search hits.
    /// Adds tokens for embedded image content; default <c>false</c>.
    /// </summary>
    public bool EnableImageUnderstanding { get; init; }
}

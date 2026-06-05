namespace Zonit.Extensions.Ai.Anthropic.Tools;

/// <summary>
/// Anthropic <c>web_search_20250305</c> server tool. Unlike OpenAI's variant
/// this one exposes a hard <see cref="MaxUses"/> ceiling, allow / block-list
/// domain filters, and an explicit approximate <c>user_location</c> hint.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="MaxUses"/> is exceeded the API returns a
/// <c>web_search_tool_result</c> error with the <c>max_uses_exceeded</c>
/// code instead of running another search.
/// </para>
/// <para>
/// Anthropic also ships a newer <c>web_search_20260209</c> variant with
/// dynamic, code-driven filtering. We will add a separate tool class for it
/// once the wire format stabilises and we can reasonably wire the response
/// content blocks.
/// </para>
/// </remarks>
public class WebSearchTool : IAnthropicTool
{
    /// <summary>Hard limit on the number of search invocations per request.</summary>
    public int? MaxUses { get; init; }

    /// <summary>Allow-list of domains the model is permitted to draw from.</summary>
    public IReadOnlyList<string>? AllowedDomains { get; init; }

    /// <summary>Block-list of domains never used as search results.</summary>
    public IReadOnlyList<string>? BlockedDomains { get; init; }

    /// <summary>City forwarded as <c>user_location.city</c>.</summary>
    public string? City { get; init; }
    /// <summary>Region / state forwarded as <c>user_location.region</c>.</summary>
    public string? Region { get; init; }
    /// <summary>Country forwarded as <c>user_location.country</c>.</summary>
    public string? Country { get; init; }
    /// <summary>IANA timezone ID forwarded as <c>user_location.timezone</c>.</summary>
    public string? TimeZone { get; init; }
}

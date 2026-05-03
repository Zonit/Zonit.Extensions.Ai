namespace Zonit.Extensions.Ai.OpenAi.Tools;

/// <summary>
/// OpenAI Responses API <c>web_search</c> server tool. Configurable
/// <see cref="ContextSize"/> (low / medium / high) trades off result depth
/// against token cost; optional <see cref="Country"/> / <see cref="Region"/>
/// / <see cref="City"/> / <see cref="TimeZone"/> are forwarded as approximate
/// <c>user_location</c> hints.
/// </summary>
/// <remarks>
/// OpenAI exposes web search exclusively on the Responses API and bills per
/// invocation in addition to the model's token cost. See the OpenAI tools
/// pricing page for current rates.
/// </remarks>
public class WebSearchTool : IOpenAiTool
{
    /// <summary>ISO country code passed as approximate user_location.country.</summary>
    public string? Country { get; init; }
    /// <summary>Region / state passed as approximate user_location.region.</summary>
    public string? Region { get; init; }
    /// <summary>City passed as approximate user_location.city.</summary>
    public string? City { get; init; }
    /// <summary>IANA timezone ID passed as approximate user_location.timezone.</summary>
    public string? TimeZone { get; init; }

    /// <summary>
    /// How much retrieved page content the model is allowed to consume.
    /// Larger sizes improve grounding at the cost of more input tokens.
    /// </summary>
    public ContextSizeType ContextSize { get; set; } = ContextSizeType.Medium;

    public enum ContextSizeType
    {
        Low,
        Medium,
        High,
    }
}

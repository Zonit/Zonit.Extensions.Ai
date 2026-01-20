namespace Zonit.Extensions;

/// <summary>
/// X (Grok) AI provider configuration options.
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai:X"</c>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "X": {
///       "ApiKey": "xai-...",
///       "BaseUrl": "https://api.x.ai"
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class XOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:X";
}

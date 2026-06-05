namespace Zonit.Extensions;

/// <summary>
/// Google Gemini provider configuration options.
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai:Google"</c>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "Google": {
///       "ApiKey": "AIza...",
///       "BaseUrl": "https://generativelanguage.googleapis.com"
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class GoogleOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:Google";
}

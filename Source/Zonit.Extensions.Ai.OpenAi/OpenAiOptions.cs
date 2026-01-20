namespace Zonit.Extensions;

/// <summary>
/// OpenAI provider configuration options.
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai:OpenAi"</c>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "OpenAi": {
///       "ApiKey": "sk-...",
///       "OrganizationId": "org-...",
///       "BaseUrl": "https://api.openai.com"
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class OpenAiOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:OpenAi";

    /// <summary>
    /// OpenAI Organization ID (optional).
    /// </summary>
    /// <remarks>
    /// Required only if you belong to multiple organizations and need to specify which one to use.
    /// </remarks>
    public string? OrganizationId { get; set; }
}

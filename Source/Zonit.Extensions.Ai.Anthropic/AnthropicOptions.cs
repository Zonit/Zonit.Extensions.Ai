namespace Zonit.Extensions;

/// <summary>
/// Anthropic Claude provider configuration options.
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai:Anthropic"</c>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "Anthropic": {
///       "ApiKey": "sk-ant-...",
///       "BaseUrl": "https://api.anthropic.com"
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AnthropicOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:Anthropic";
}

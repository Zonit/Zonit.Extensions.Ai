namespace Zonit.Extensions;

/// <summary>
/// ElevenLabs provider configuration options.
/// </summary>
/// <remarks>
/// Configuration section: <c>"Ai:ElevenLabs"</c>
/// <para>
/// Example appsettings.json:
/// <code>
/// {
///   "Ai": {
///     "ElevenLabs": {
///       "ApiKey": "sk_..."
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class ElevenLabsOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:ElevenLabs";
}

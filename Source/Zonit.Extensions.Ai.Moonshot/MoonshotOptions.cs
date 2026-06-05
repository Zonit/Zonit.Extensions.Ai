namespace Zonit.Extensions.Ai.Moonshot;

/// <summary>
/// Configuration options for Moonshot AI provider (Kimi).
/// </summary>
public sealed class MoonshotOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:Moonshot";

    /// <summary>
    /// Initializes a new instance of Moonshot options with default base URL.
    /// </summary>
    public MoonshotOptions()
    {
        BaseUrl = "https://api.moonshot.cn";
    }
}

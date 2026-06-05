namespace Zonit.Extensions.Ai.Yi;

/// <summary>
/// Configuration options for 01.AI (Yi) provider.
/// </summary>
public sealed class YiOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ai:Yi";

    /// <summary>
    /// Initializes a new instance of Yi options with default base URL.
    /// </summary>
    public YiOptions()
    {
        BaseUrl = "https://api.01.ai";
    }
}

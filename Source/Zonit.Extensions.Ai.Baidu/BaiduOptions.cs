namespace Zonit.Extensions.Ai.Baidu;

/// <summary>
/// Baidu AI (Qianfan) provider options.
/// </summary>
public sealed class BaiduOptions : AiProviderOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Ai:Baidu";

    /// <summary>
    /// Baidu Secret Key for authentication.
    /// </summary>
    public string? SecretKey { get; set; }
}

namespace Zonit.Extensions.Ai;

/// <summary>
/// Marks a class as an AI provider implementation.
/// Used for provider identification and metadata purposes.
/// Providers must be registered explicitly via their extension methods
/// (e.g., <c>AddAiOpenAi()</c>) to ensure proper HttpClient configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AiProviderAttribute : Attribute
{
    /// <summary>
    /// Provider identifier (e.g., "openai", "anthropic", "google", "x").
    /// </summary>
    public string ProviderId { get; }

    /// <summary>
    /// Creates a new provider attribute.
    /// </summary>
    /// <param name="providerId">Unique provider identifier.</param>
    public AiProviderAttribute(string providerId)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
    }
}

namespace Zonit.Extensions.Ai;

/// <summary>
/// Marks a class as an AI provider for auto-discovery.
/// The Source Generator will detect classes with this attribute
/// and register them automatically with DI.
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

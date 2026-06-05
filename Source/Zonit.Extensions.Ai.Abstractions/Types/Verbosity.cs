namespace Zonit.Extensions.Ai;

/// <summary>
/// Output verbosity control for models that support it.
/// </summary>
public enum Verbosity
{
    /// <summary>
    /// Concise responses with minimal elaboration.
    /// </summary>
    Low,

    /// <summary>
    /// Balanced detail and brevity (default).
    /// </summary>
    Medium,

    /// <summary>
    /// Detailed, comprehensive responses with full elaboration.
    /// </summary>
    High
}

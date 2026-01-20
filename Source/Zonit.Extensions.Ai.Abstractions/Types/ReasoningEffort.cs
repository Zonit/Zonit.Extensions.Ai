namespace Zonit.Extensions.Ai;

/// <summary>
/// Reasoning effort level for reasoning models.
/// </summary>
public enum ReasoningEffort
{
    /// <summary>
    /// No reasoning effort (some models default). Fastest response.
    /// </summary>
    None,
    
    /// <summary>
    /// Light reasoning with quick judgment. Fast response with moderate accuracy.
    /// </summary>
    Low,
    
    /// <summary>
    /// Balanced depth vs speed. Safe general-purpose choice.
    /// </summary>
    Medium,
    
    /// <summary>
    /// Deep, multistep reasoning for complex problems. Slowest but highest accuracy.
    /// </summary>
    High
}

using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Result of an AI operation with value and metadata.
/// </summary>
/// <typeparam name="T">The result value type.</typeparam>
public sealed class AiResult<T>
{
    /// <summary>
    /// The generated value (strongly typed).
    /// </summary>
    public required T Value { get; init; }
    
    /// <summary>
    /// Token usage and cost information.
    /// </summary>
    public required TokenUsage Usage { get; init; }
    
    /// <summary>
    /// The model that was used.
    /// </summary>
    public required string Model { get; init; }
    
    /// <summary>
    /// The provider that handled the request.
    /// </summary>
    public required string Provider { get; init; }
    
    /// <summary>
    /// The prompt name for statistics tracking.
    /// Automatically derived from prompt class name (e.g., "TranslatePrompt" -> "Translate").
    /// </summary>
    public required string PromptName { get; init; }
    
    /// <summary>
    /// Request duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Provider-specific request ID.
    /// </summary>
    public string? RequestId { get; init; }
    
    /// <summary>
    /// Total cost of the operation.
    /// Calculated from token usage and model pricing.
    /// </summary>
    public Price TotalCost => Usage.TotalCost;
    
    /// <summary>
    /// Cost of input/prompt tokens.
    /// </summary>
    public Price InputCost => Usage.InputCost;
    
    /// <summary>
    /// Cost of output/completion tokens.
    /// </summary>
    public Price OutputCost => Usage.OutputCost;
}

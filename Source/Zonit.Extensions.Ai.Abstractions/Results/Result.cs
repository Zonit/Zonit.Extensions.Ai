namespace Zonit.Extensions.Ai;

/// <summary>
/// Result of an AI operation containing the generated value and metadata.
/// </summary>
/// <typeparam name="T">The result value type.</typeparam>
public sealed class Result<T>
{
    /// <summary>
    /// The generated value (strongly typed).
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// Metadata about the AI operation (model, usage, costs, duration, etc.).
    /// </summary>
    public required MetaData MetaData { get; init; }
}

/// <summary>
/// Metadata about an AI operation including model, usage, costs, and timing.
/// </summary>
public sealed class MetaData
{
    /// <summary>
    /// The model that was used for the operation.
    /// </summary>
    public required ILlm Model { get; init; }

    /// <summary>
    /// The provider that handled the request (e.g., "OpenAI", "Anthropic").
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// The prompt name for statistics tracking.
    /// Automatically derived from prompt class name (e.g., "TranslatePrompt" -> "Translate").
    /// </summary>
    public required string PromptName { get; init; }

    /// <summary>
    /// Token usage information.
    /// </summary>
    public required TokenUsage Usage { get; init; }

    /// <summary>
    /// Request duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Provider-specific request ID.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Input tokens count (shortcut for Usage.InputTokens).
    /// </summary>
    public int InputTokens => Usage.InputTokens;

    /// <summary>
    /// Output tokens count (shortcut for Usage.OutputTokens).
    /// </summary>
    public int OutputTokens => Usage.OutputTokens;

    /// <summary>
    /// Total tokens count (shortcut for Usage.TotalTokens).
    /// </summary>
    public int TotalTokens => Usage.TotalTokens;

    /// <summary>
    /// Cost of input/prompt tokens.
    /// </summary>
    public Price InputCost => Usage.InputCost;

    /// <summary>
    /// Cost of output/completion tokens.
    /// </summary>
    public Price OutputCost => Usage.OutputCost;

    /// <summary>
    /// Total cost of the operation (input + output).
    /// </summary>
    public Price TotalCost => Usage.TotalCost;
}

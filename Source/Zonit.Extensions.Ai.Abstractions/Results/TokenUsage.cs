using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Token usage and cost information for an AI operation.
/// </summary>
public sealed class TokenUsage
{
    /// <summary>
    /// Input/prompt tokens.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Output/completion tokens.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Total tokens (input + output).
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Cached input tokens read from cache (if supported by provider).
    /// Cached tokens are typically cheaper than regular input tokens.
    /// </summary>
    public int CachedTokens { get; init; }

    /// <summary>
    /// Tokens written to cache (Anthropic prompt caching).
    /// Cache writes are more expensive than regular input tokens (1.25x for 5-min TTL).
    /// </summary>
    public int CacheWriteTokens { get; init; }

    /// <summary>
    /// Reasoning/thinking tokens (for reasoning models like o1, o3, Gemini thinking).
    /// Already included in OutputTokens; stored separately for informational purposes.
    /// </summary>
    public int ReasoningTokens { get; init; }

    /// <summary>
    /// Cost of input tokens.
    /// </summary>
    public Price InputCost { get; init; }

    /// <summary>
    /// Cost of output tokens.
    /// </summary>
    public Price OutputCost { get; init; }

    /// <summary>
    /// Total cost (input + output).
    /// </summary>
    public Price TotalCost => InputCost + OutputCost;
}

/// <summary>
/// Legacy Usage class for backward compatibility.
/// Use <see cref="TokenUsage"/> instead.
/// </summary>
[Obsolete("Use TokenUsage instead. This class will be removed in a future version.")]
public sealed class Usage
{
    /// <summary>
    /// Input tokens count.
    /// </summary>
    public int Input { get; init; }

    /// <summary>
    /// Output tokens count.
    /// </summary>
    public int Output { get; init; }

    /// <summary>
    /// Total tokens count.
    /// </summary>
    public int Total => Input + Output;

    /// <summary>
    /// Converts to TokenUsage.
    /// </summary>
    public TokenUsage ToTokenUsage() => new()
    {
        InputTokens = Input,
        OutputTokens = Output
    };

    /// <summary>
    /// Implicit conversion to TokenUsage.
    /// </summary>
    public static implicit operator TokenUsage(Usage usage) => usage.ToTokenUsage();
}

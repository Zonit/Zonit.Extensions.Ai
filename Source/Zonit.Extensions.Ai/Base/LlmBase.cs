namespace Zonit.Extensions.Ai;

/// <summary>
/// Base class for all LLM implementations with common functionality.
/// </summary>
public abstract class LlmBase : ILlm
{
    private int? _maxTokens;

    /// <inheritdoc />
    public virtual int MaxTokens
    {
        // Default to the model's full output capacity instead of a fixed 1024
        // floor, so a model is never silently capped below what it can emit
        // (e.g. Opus 4.8 must default to its 128k output, not 1024). Embedding /
        // image / audio models report MaxOutputTokens = 0; keep the legacy 1024
        // fallback for them since max_tokens is meaningless on those endpoints.
        get => _maxTokens ?? (MaxOutputTokens > 0 ? MaxOutputTokens : 1024);
        init
        {
            if (value > MaxInputTokens + MaxOutputTokens)
                throw new ArgumentOutOfRangeException(nameof(MaxTokens),
                    $"MaxTokens ({value}) cannot exceed the sum of MaxInputTokens ({MaxInputTokens}) and MaxOutputTokens ({MaxOutputTokens})");
            _maxTokens = value;
        }
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract decimal PriceInput { get; }

    /// <inheritdoc />
    public abstract decimal PriceOutput { get; }

    /// <inheritdoc />
    public virtual decimal? BatchPriceInput { get; } = null;

    /// <inheritdoc />
    public virtual decimal? BatchPriceOutput { get; } = null;

    /// <inheritdoc />
    public abstract int MaxInputTokens { get; }

    /// <inheritdoc />
    public abstract int MaxOutputTokens { get; }

    /// <inheritdoc />
    public abstract ChannelType Input { get; }

    /// <inheritdoc />
    public abstract ChannelType Output { get; }

    /// <inheritdoc />
    public virtual ToolsType SupportedTools { get; } = ToolsType.None;

    /// <inheritdoc />
    public virtual FeaturesType SupportedFeatures { get; } = FeaturesType.None;

    /// <inheritdoc />
    public virtual EndpointsType SupportedEndpoints { get; } = EndpointsType.None;

    /// <inheritdoc />
    public virtual decimal GetInputPrice(long tokenCount) => PriceInput;

    /// <inheritdoc />
    public virtual decimal GetOutputPrice(long tokenCount) => PriceOutput;

    /// <inheritdoc />
    public virtual IToolBase[]? Tools { get; init; } = null;
}

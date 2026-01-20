namespace Zonit.Extensions.Ai;

/// <summary>
/// Base class for all LLM implementations with common functionality.
/// </summary>
public abstract class LlmBase : ILlm
{
    private int _maxTokens = 1024;

    /// <inheritdoc />
    public virtual int MaxTokens
    {
        get => _maxTokens;
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

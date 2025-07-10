namespace Zonit.Extensions.Ai.Llm;

public abstract class LlmBase : ILlmBase
{
    private int _maxTokens = 1024;

    public virtual int MaxTokens
    {
        get => _maxTokens;
        init
        {
            if (value > MaxInputTokens + MaxOutputTokens)
                throw new ArgumentOutOfRangeException(nameof(MaxTokens), $"MaxTokens ({value}) cannot exceed the sum of MaxInputTokens ({MaxInputTokens}) and MaxOutputTokens ({MaxOutputTokens})");

            _maxTokens = value;
        }
    }

    public abstract string Name { get; }

    public abstract decimal PriceInput { get; }
    public abstract decimal PriceOutput { get; }

    public virtual decimal? BatchPriceInput { get; } = null;
    public virtual decimal? BatchPriceOutput { get; } = null;

    public abstract int MaxInputTokens { get; }
    public abstract int MaxOutputTokens { get; }

    public abstract ChannelType Input { get; }
    public abstract ChannelType Output { get; } 

    public virtual ToolsType Tools { get; } = ToolsType.None;
    public virtual FeaturesType Features { get; } = FeaturesType.None;
    public virtual EndpointsType Endpoints { get; } = EndpointsType.None;

    public virtual decimal GetInputPrice(long tokenCount) => PriceInput;
    public virtual decimal GetOutputPrice(long tokenCount) => PriceOutput;
}
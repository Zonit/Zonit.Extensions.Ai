namespace Zonit.Extensions.Ai;

public abstract class BaseModel : IBaseModel
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

    public abstract bool InputText { get; }
    public abstract bool InputImage { get; }
    public abstract bool InputAudio { get; }

    public abstract bool OutputText { get; }
    public abstract bool OutputImage { get; }
    public abstract bool OutputAudio { get; }
}
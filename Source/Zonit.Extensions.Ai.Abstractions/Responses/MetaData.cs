namespace Zonit.Extensions.Ai;

public class MetaData(IBaseModel model, Usage usage, TimeSpan? duration = default)
{
    public IBaseModel Model { get; } = model;
    public Usage Usage { get; } = usage;

    public long InputTokenCount => Usage.Input;
    public long OutputTokenCount => Usage.Output;

    // Price per 1,000,000 tokens
    public decimal PriceInput => (Model.PriceInput * InputTokenCount) / 1_000_000m;
    public decimal PriceOutput => (Model.PriceOutput * OutputTokenCount) / 1_000_000m;
    public decimal PriceTotal => PriceInput + PriceOutput;

    /// <summary>
    /// Czas trwania generowania treści
    /// </summary>
    public TimeSpan Duration { get; } = duration ?? TimeSpan.Zero;
}
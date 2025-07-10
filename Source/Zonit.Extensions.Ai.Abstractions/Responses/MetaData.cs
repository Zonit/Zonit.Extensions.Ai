using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai;

public class MetaData(ILlmBase model, Usage usage, TimeSpan? duration = default)
{
    public ILlmBase Model { get; } = model;
    public Usage Usage { get; } = usage;

    public long InputTokenCount => Usage.Input;
    public long OutputTokenCount => Usage.Output;

    // Price per 1,000,000 tokens - używa dynamicznych cen z modelu
    public decimal PriceInput => (Model.GetInputPrice(InputTokenCount) * InputTokenCount) / 1_000_000m;
    public decimal PriceOutput => (Model.GetOutputPrice(OutputTokenCount) * OutputTokenCount) / 1_000_000m;
    public decimal PriceTotal => PriceInput + PriceOutput;

    /// <summary>
    /// Czas trwania generowania treści
    /// </summary>
    public TimeSpan Duration { get; } = duration ?? TimeSpan.Zero;

    /// <summary>
    /// Nazwa procesu który został wykonany
    /// </summary>
    public string? Process { get; internal set; }
}
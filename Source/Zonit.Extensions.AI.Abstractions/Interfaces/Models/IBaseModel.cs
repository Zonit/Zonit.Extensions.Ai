namespace Zonit.Extensions.AI;

public interface IBaseModel
{
    int MaxTokens { get; init; }
    string Name { get; }
    decimal PriceInput { get; }
    decimal PriceOutput { get; }
    decimal? BatchPriceInput { get; }
    decimal? BatchPriceOutput { get; }
    int MaxInputTokens { get; }
    int MaxOutputTokens { get; }
    bool InputText { get; }
    bool InputImage { get; }
    bool InputAudio { get; }
    bool OutputText { get; }
    bool OutputImage { get; }
    bool OutputAudio { get; }
}

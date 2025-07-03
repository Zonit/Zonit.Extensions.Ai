namespace Zonit.Extensions.Ai.Llm;

public interface IImageLlmBase : ILlmBase
{
    string QualityValue { get; }
    string SizeValue { get; }
    int Quantity { get; }
}
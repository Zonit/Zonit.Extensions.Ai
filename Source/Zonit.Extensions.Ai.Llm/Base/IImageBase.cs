namespace Zonit.Extensions.Ai.Llm;

public interface IImageBase : ILlmBase
{
    string QualityValue { get; }
    string SizeValue { get; }
    int Quantity { get; }
}
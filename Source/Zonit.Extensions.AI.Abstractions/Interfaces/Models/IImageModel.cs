namespace Zonit.Extensions.Ai;

public interface IImageModel : IBaseModel
{
    string QualityValue { get; }
    string SizeValue { get; }
    int Quantity { get; }
}
namespace Zonit.Extensions.Ai;

public class GPTImage1 : BaseOpenAiImage<GPTImage1.QualityType, GPTImage1.SizeType>
{
    public required override QualityType Quality { get; init; }
    public required override SizeType Size { get; init; }
    public override string Name => "gpt-image-1";

    public override decimal PriceInput => 10;
    public override decimal PriceOutput => 40;

    public override int MaxInputTokens => throw new NotImplementedException();
    public override int MaxOutputTokens => throw new NotImplementedException();

    public override bool InputText => true;
    public override bool InputImage => true;
    public override bool InputAudio => true;
    public override bool OutputText => false;
    public override bool OutputImage => true;
    public override bool OutputAudio => false;

    public enum QualityType
    {
        [EnumValue("auto")]
        Auto,

        [EnumValue("low")]
        Low,

        [EnumValue("medium")]
        Medium,

        [EnumValue("high")]
        High,
    }

    public enum SizeType
    {

        [EnumValue("auto")]
        Auto,

        [EnumValue("1024x1024")]
        Square,

        [EnumValue("1536x1024")]
        Landscape,

        [EnumValue("1024x1536")]
        Portrait
    }
}
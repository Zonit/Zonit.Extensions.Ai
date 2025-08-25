namespace Zonit.Extensions.Ai.Llm.OpenAi;

public class GPTImage1 : OpenAiImageBase<GPTImage1.QualityType, GPTImage1.SizeType>
{
    public required override QualityType Quality { get; init; }
    public required override SizeType Size { get; init; }
    public override string Name => "gpt-image-1";

    public override decimal PriceInput => 10;
    public override decimal PriceOutput => 40;

    public override int MaxInputTokens => throw new NotImplementedException();
    public override int MaxOutputTokens => throw new NotImplementedException();

    public override ChannelType Input => ChannelType.Text | ChannelType.Image;
    public override ChannelType Output => ChannelType.Image;

    public override ToolsType SupportedTools => ToolsType.None;
    public override FeaturesType SupportedFeatures => FeaturesType.Inpainting;
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Image |
        EndpointsType.ImageEdit;

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
namespace Zonit.Extensions.AI;

public class GPT41 : BaseOpenAITextModel
{
    public override string Name => "gpt-4.1-2025-04-14";
    public override decimal? PriceCachedInput => 0.50m;
    public override decimal PriceInput => 2.00m;
    public override decimal PriceOutput => 8.00m;
    public override decimal? BatchPriceInput => 1.00m;
    public override decimal? BatchPriceOutput => 4.00m;

    public override int MaxInputTokens => 1047576;
    public override int MaxOutputTokens => 32768;

    public override bool InputText => true;
    public override bool InputImage => true;
    public override bool InputAudio => false;

    public override bool OutputText => true;
    public override bool OutputImage => false;
    public override bool OutputAudio => false;
}
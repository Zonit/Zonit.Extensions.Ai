﻿namespace Zonit.Extensions.Ai;

public class GPT41Mini : BaseOpenAiText
{
    public override string Name => "gpt-4.1-mini-2025-04-14";

    public override decimal? PriceCachedInput => 0.10m;
    public override decimal PriceInput => 0.40m;
    public override decimal PriceOutput => 1.60m;
    public override decimal? BatchPriceInput => 0.20m;
    public override decimal? BatchPriceOutput => 0.80m;

    public override int MaxInputTokens => 1047576;
    public override int MaxOutputTokens => 32768;

    public override bool InputText => true;
    public override bool InputImage => true;
    public override bool InputAudio => false;

    public override bool OutputText => true;
    public override bool OutputImage => false;
    public override bool OutputAudio => false;
}
namespace Zonit.Extensions.Ai.Llm.X;

public class Grok4 : XReasoningBase
{
    public override string Name => "grok-4-0709";

    public override decimal PriceInput => 3.00m;

    public override decimal PriceCachedInput => 0.75m;

    public override decimal PriceOutput => 15.00m;

    public override int MaxInputTokens => 256_000;

    public override int MaxOutputTokens => 8_192;

    public override ChannelType Input => ChannelType.Text;

    public override ChannelType Output => ChannelType.Text;

    public override ToolsType Tools => ToolsType.WebSearch;

    public override EndpointsType Endpoints => EndpointsType.Chat;

    /// <summary>
    /// Grok4 ma podw�jn� cen� dla kontekst�w powy�ej 128k token�w
    /// </summary>
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 128_000 ? PriceInput * 2 : PriceInput;
    }

    /// <summary>
    /// Cached input r�wnie� ma podw�jn� cen� powy�ej 128k token�w
    /// </summary>
    public override decimal GetOutputPrice(long tokenCount)
    {
        return tokenCount > 128_000 ? PriceCachedInput * 2 : PriceCachedInput;
    }
}
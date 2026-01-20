namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT Image 1 Mini - A cost-efficient version of GPT Image 1.
/// </summary>
public class GPTImage1Mini : OpenAiImageBase
{
    /// <inheritdoc />
    public override string Name => "gpt-image-1-mini";

    /// <inheritdoc />
    public override decimal PriceInput => 0m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0m;

    /// <inheritdoc />
    public override int MaxInputTokens => 32_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 0;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Image;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures => FeaturesType.None;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Image;
}

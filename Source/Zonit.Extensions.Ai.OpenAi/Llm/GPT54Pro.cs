namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.4 Pro - Most powerful GPT-5.4 variant for the most demanding tasks.
/// Produces smarter and more precise responses at premium pricing.
/// </summary>
public class GPT54Pro : OpenAiReasoningBase
{
    /// <inheritdoc />
    public override string Name => "gpt-5.4-pro";

    /// <inheritdoc />
    public override decimal PriceInput => 30.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 180.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => null;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => 15.00m;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => 90.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_100_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 128_000;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools =>
        ToolsType.WebSearch |
        ToolsType.FileSearch |
        ToolsType.ImageGeneration |
        ToolsType.CodeInterpreter |
        ToolsType.MCP;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Assistant |
        EndpointsType.Batch;
}

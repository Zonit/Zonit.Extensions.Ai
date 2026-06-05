namespace Zonit.Extensions.Ai.Together;

/// <summary>
/// Meta Llama 3.3 70B Instruct on Together AI.
/// Latest Llama model with excellent performance.
/// </summary>
public class MetaLlama3_3_70BInstruct : TogetherBase
{
    /// <inheritdoc />
    public override string Name => "meta-llama/Llama-3.3-70B-Instruct-Turbo";

    /// <inheritdoc />
    public override decimal PriceInput => 0.88m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.88m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 4_096;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

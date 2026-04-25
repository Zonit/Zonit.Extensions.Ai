namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5 Chat - GPT-5 model used in ChatGPT.
/// </summary>
public class GPT5Chat : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5-chat-latest";

    /// <inheritdoc />
    public override decimal PriceInput => 1.25m;

    /// <inheritdoc />
    public override decimal PriceOutput => 10.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.125m;

    /// <inheritdoc />
    public override decimal? BatchPriceInput => null;

    /// <inheritdoc />
    public override decimal? BatchPriceOutput => null;

    /// <inheritdoc />
    public override int MaxInputTokens => 400_000;

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
        EndpointsType.Response;
}

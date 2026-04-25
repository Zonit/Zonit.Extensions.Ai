namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.2 Codex - Most intelligent coding model optimized for long-horizon, agentic coding tasks.
/// </summary>
public class GPT52Codex : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.2-codex";

    /// <inheritdoc />
    public override decimal PriceInput => 1.75m;

    /// <inheritdoc />
    public override decimal PriceOutput => 14.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.175m;

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
        ToolsType.FileSearch |
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
        EndpointsType.Batch;
}

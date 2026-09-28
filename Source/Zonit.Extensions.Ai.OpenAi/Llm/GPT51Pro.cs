namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.1 Pro - Version of GPT-5.1 that produces smarter and more precise responses.
/// </summary>
[Obsolete("Superseded by Sol6 (gpt-6-sol), or Astra6 (gpt-6-astra) for the hardest workloads. Still works — upgrade for better quality.")]
public class GPT51Pro : OpenAiReasoningBase<OpenAiReasonEffort>, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.1-pro";

    /// <inheritdoc />
    public override decimal PriceInput => 5.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 20.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 1.25m;

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
        EndpointsType.Response |
        EndpointsType.Assistant |
        EndpointsType.Batch;
}

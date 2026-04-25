namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.1 - Improved GPT-5 with extended reasoning capabilities.
/// Note: Unlike other reasoning models, GPT-5.1 defaults to ReasoningEffort.None.
/// To enable reasoning, explicitly set the Reason property.
/// GPT-5 models do NOT support temperature, top_p, or logprobs parameters.
/// </summary>
public class GPT51 : OpenAiReasoningBase, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.1-2025-11-13";

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
    public override ChannelType Output => ChannelType.Text | ChannelType.Image;

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
        FeaturesType.StructuredOutputs |
        FeaturesType.Distillation;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response;
}

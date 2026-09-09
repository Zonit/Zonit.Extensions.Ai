namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-5.6 Cyber — the cybersecurity member of the GPT-5.6 family, for approved
/// defenders doing authorized vulnerability research, exploit validation and
/// security testing.
/// </summary>
/// <remarks>
/// <para>
/// Model id <c>gpt-5.6-cyber</c>. 400K context window, 272K max input, 128K max
/// output, knowledge cutoff 16 February 2026.
/// </para>
/// <para>
/// <b>Gated model:</b> requires separate approval through OpenAI's Daybreak
/// program — an ordinary API key returns a model-not-found error. Only the
/// Responses endpoint is supported; Chat Completions and Batch are not.
/// </para>
/// <para>
/// Max input sits at the long-context threshold, so no long-context surcharge
/// tier applies.
/// </para>
/// </remarks>
public class Cyber56 : OpenAiReasoningBase<OpenAiReasonEffortExtended>, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-5.6-cyber";

    /// <inheritdoc />
    public override decimal PriceInput => 12.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 75.00m;

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
        ToolsType.MCP;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Response;
}

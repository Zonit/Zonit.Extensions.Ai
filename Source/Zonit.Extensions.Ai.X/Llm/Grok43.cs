namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.3 — xAI's recommended general-purpose model. Most truth-seeking
/// large language model in the catalog and the default suggested by xAI for
/// every text/agent workload that doesn't explicitly need multi-agent or fast
/// tiers.
/// </summary>
/// <remarks>
/// <para>
/// Pricing: $1.25 / $2.50 per 1M tokens, $0.3125 cached input. 1M context window.
/// </para>
/// <para>
/// Reasoning is automatic — <c>reasoning_effort</c> is rejected by the API.
/// See <see href="https://docs.x.ai/developers/models/grok-4.3"/>.
/// </para>
/// </remarks>
public class Grok43 : XReasoningBase
{
    /// <inheritdoc />
    public override string Name => "grok-4.3";

    /// <inheritdoc />
    public override decimal PriceInput => 1.25m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.3125m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.50m;

    /// <inheritdoc />
    /// <remarks>1M context window.</remarks>
    public override int MaxInputTokens => 1_000_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 131_072;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools =>
        ToolsType.WebSearch |
        ToolsType.CodeExecution;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat | EndpointsType.Response;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.Reasoning;
}

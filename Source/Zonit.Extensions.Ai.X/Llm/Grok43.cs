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
/// Supports the <c>reasoning.effort</c> parameter ∈ { <c>none</c>, <c>low</c>,
/// <c>medium</c>, <c>high</c>, <c>xhigh</c> }; xAI defaults to <c>low</c> when omitted. Set
/// <see cref="Reason"/> to override (use <c>none</c> to
/// disable thinking entirely). Reasoning summaries are emitted by xAI
/// automatically — no client-side toggle is required.
/// </para>
/// <para>
/// See <see href="https://docs.x.ai/developers/model-capabilities/text/reasoning"/>
/// and <see href="https://docs.x.ai/developers/models/grok-4.3"/>.
/// </para>
/// </remarks>
[Obsolete("Superseded by Grok47 (grok-4.7), xAI's current frontier model. Still works — upgrade for better quality.")]
public class Grok43 : XChatBase, IReasoningLlm
{
    /// <summary>
    /// Reasoning levels accepted by <c>grok-4.3</c> (<c>none</c> / <c>low</c> / <c>medium</c> / <c>high</c> / <c>xhigh</c>). Levels the API rejects
    /// (<c>max</c>) are not members, so passing one is a compile-time error rather than an
    /// HTTP 400. Numeric values align with <see cref="ReasoningEffort"/>.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>No reasoning — the model answers directly. Wire value <c>"none"</c>.</summary>
        None = 0,
        /// <summary>Light reasoning — fastest, lowest cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning.</summary>
        High = 3,
        /// <summary>Extra effort above <see cref="High"/>. Wire value <c>"xhigh"</c>.</summary>
        Extra = 4,
    }

    /// <summary>
    /// Thinking effort. <c>null</c> lets xAI pick the default (<c>low</c>).
    /// </summary>
    public ReasonType? Reason { get; init; }

    /// <inheritdoc />
    ReasoningEffort? IReasoningLlm.Reason => Reason is { } reason ? (ReasoningEffort)reason : null;

    /// <inheritdoc />
    /// <remarks>
    /// xAI emits reasoning summaries automatically for grok-4.3 — no
    /// client-side toggle. Always returns <c>null</c>.
    /// </remarks>
    ReasoningSummary? IReasoningLlm.ReasonSummary => null;

    /// <inheritdoc />
    /// <remarks>grok-4.3 does not expose a verbosity knob.</remarks>
    Verbosity? IReasoningLlm.OutputVerbosity => null;

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
        ToolsType.XSearch |
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

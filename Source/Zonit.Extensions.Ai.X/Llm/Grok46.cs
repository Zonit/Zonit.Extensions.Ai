namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.6 — xAI's frontier model, built for coding, agentic tasks and
/// knowledge work. Successor to <see cref="Grok45"/>: same $2 / $6 headline
/// pricing and 500K context, plus a fourth reasoning level
/// (<see cref="ReasoningEffort.Extra"/>, wire <c>xhigh</c>) that grok-4.5
/// does not accept.
/// </summary>
/// <remarks>
/// <para>
/// Pricing is <b>tiered on prompt size</b>: $2.00 input / $0.50 cached input /
/// $6.00 output per 1M tokens below 200K prompt tokens, and <b>double that</b>
/// ($4.00 / $1.00 / $12.00) from 200K up. The higher rate applies to
/// <i>every</i> token in the request, not just the tokens past the threshold —
/// see <see cref="GetInputPrice"/>. Note the cached-read rate is tiered too,
/// which is why this class overrides <see cref="GetCachedInputPrice"/>.
/// </para>
/// <para>
/// Supports the <c>reasoning.effort</c> parameter ∈ { <c>low</c>, <c>medium</c>,
/// <c>high</c>, <c>xhigh</c> }; xAI defaults to <c>high</c> when omitted. Set
/// <see cref="Reason"/> to override. Like <see cref="Grok45"/>, grok-4.6 does
/// not accept <see cref="ReasoningEffort.None"/> — leave <see cref="Reason"/>
/// null to take the <c>high</c> default. Reasoning summaries are emitted by
/// xAI automatically — no client-side toggle is required.
/// </para>
/// <para>
/// xAI publishes no output-token ceiling for grok-4.6;
/// <see cref="MaxOutputTokens"/> keeps grok-4.5's 131,072 as the request-side
/// default. Knowledge cutoff: February 1, 2026.
/// </para>
/// <para>
/// See <see href="https://docs.x.ai/developers/grok-4-6"/> and
/// <see href="https://docs.x.ai/docs/pricing"/>.
/// </para>
/// </remarks>
public class Grok46 : XChatBase, IReasoningLlm
{
    /// <summary>
    /// Prompt size at which xAI switches to the higher rate card. The docs read
    /// "≥ 200k", so the threshold is inclusive — a request of exactly 200,000
    /// prompt tokens already bills at the doubled rate.
    /// </summary>
    private const long LongContextThreshold = 200_000;

    /// <summary>Both sides of the rate card double past the threshold.</summary>
    private const decimal LongContextMultiplier = 2m;

    /// <summary>
    /// Thinking effort. <c>null</c> lets xAI pick the default (<c>high</c>).
    /// grok-4.6 accepts <c>low</c>, <c>medium</c>, <c>high</c> and
    /// <see cref="ReasoningEffort.Extra"/> (wire <c>xhigh</c>) —
    /// <see cref="ReasoningEffort.None"/> and <see cref="ReasoningEffort.Max"/>
    /// are rejected by the API.
    /// </summary>
    public ReasoningEffort? Reason { get; init; }

    /// <inheritdoc />
    ReasoningEffort? IReasoningLlm.Reason => Reason;

    /// <inheritdoc />
    /// <remarks>
    /// xAI emits reasoning summaries automatically for grok-4.6 — no
    /// client-side toggle. Always returns <c>null</c>.
    /// </remarks>
    ReasoningSummary? IReasoningLlm.ReasonSummary => null;

    /// <inheritdoc />
    /// <remarks>grok-4.6 does not expose a verbosity knob.</remarks>
    Verbosity? IReasoningLlm.OutputVerbosity => null;

    /// <inheritdoc />
    public override string Name => "grok-4.6";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.50m;

    /// <inheritdoc />
    public override decimal PriceOutput => 6.00m;

    /// <inheritdoc />
    /// <remarks>500K context window.</remarks>
    public override int MaxInputTokens => 500_000;

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

    /// <summary>
    /// $2.00 below the threshold, $4.00 from 200K prompt tokens up.
    /// </summary>
    public override decimal GetInputPrice(long inputTokens)
        => inputTokens >= LongContextThreshold ? PriceInput * LongContextMultiplier : PriceInput;

    /// <summary>
    /// $6.00 below the threshold, $12.00 from 200K prompt tokens up. Keyed on
    /// the <i>prompt</i> size — the output rate rises because the context is
    /// large, regardless of how long the answer is.
    /// </summary>
    public override decimal GetOutputPrice(long inputTokens, long outputTokens)
        => inputTokens >= LongContextThreshold ? PriceOutput * LongContextMultiplier : PriceOutput;

    /// <summary>
    /// $0.50 below the threshold, $1.00 from 200K prompt tokens up. Long-context
    /// agent loops are exactly where cache reads dominate, so leaving this flat
    /// would under-report their cost by half.
    /// </summary>
    public override decimal GetCachedInputPrice(long inputTokens)
        => inputTokens >= LongContextThreshold
            ? PriceCachedInputValue * LongContextMultiplier
            : PriceCachedInputValue;
}

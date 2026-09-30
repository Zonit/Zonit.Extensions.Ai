namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.5 — xAI's smartest and fastest model, recommended for coding,
/// agents, engineering and general knowledge work. Frontier reasoning at a
/// smaller context window than <see cref="Grok43"/> but higher intelligence.
/// </summary>
/// <remarks>
/// <para>
/// Pricing is <b>tiered on prompt size</b>: $2.00 input / $0.30 cached input /
/// $6.00 output per 1M tokens below 200K prompt tokens, and double that
/// ($4.00 / $0.60 / $12.00) from 200K up, applied to every token in the
/// request. 500K context window.
/// </para>
/// <para>
/// Supports the <c>reasoning.effort</c> parameter ∈ { <c>low</c>, <c>medium</c>,
/// <c>high</c> }; xAI defaults to <c>high</c> when omitted. Set <see cref="Reason"/>
/// to override. Unlike <see cref="Grok43"/>, grok-4.5 does not accept
/// <c>none</c> — leave <see cref="Reason"/> null to take the
/// <c>high</c> default. Reasoning summaries are emitted by xAI automatically — no
/// client-side toggle is required.
/// </para>
/// <para>
/// See <see href="https://docs.x.ai/developers/model-capabilities/text/reasoning"/>
/// and <see href="https://docs.x.ai/developers/grok-4-5"/>.
/// </para>
/// </remarks>
[Obsolete("Superseded by Grok47 (grok-4.7), xAI's current frontier model — same price, plus the xhigh reasoning level. Still works — upgrade for better quality.")]
public class Grok45 : XChatBase, IReasoningLlm
{
    /// <summary>
    /// Prompt size at which xAI switches to the higher rate card ("≥ 200k",
    /// so the threshold is inclusive).
    /// </summary>
    private const long LongContextThreshold = 200_000;

    /// <summary>Both sides of the rate card double past the threshold.</summary>
    private const decimal LongContextMultiplier = 2m;

    /// <summary>
    /// Reasoning levels accepted by <c>grok-4.5</c> (<c>low</c> / <c>medium</c> / <c>high</c>). Levels the API rejects
    /// (<c>none</c>, <c>xhigh</c>, <c>max</c>) are not members, so passing one is a compile-time error rather than an
    /// HTTP 400. Numeric values align with <see cref="ReasoningEffort"/>.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>Light reasoning — fastest, lowest cost.</summary>
        Low = 1,
        /// <summary>Balanced reasoning depth.</summary>
        Medium = 2,
        /// <summary>Deep multistep reasoning.</summary>
        High = 3,
    }

    /// <summary>
    /// Thinking effort. <c>null</c> lets xAI pick the default (<c>high</c>).
    /// </summary>
    public ReasonType? Reason { get; init; }

    /// <inheritdoc />
    ReasoningEffort? IReasoningLlm.Reason => Reason is { } reason ? (ReasoningEffort)reason : null;

    /// <inheritdoc />
    /// <remarks>
    /// xAI emits reasoning summaries automatically for grok-4.5 — no
    /// client-side toggle. Always returns <c>null</c>.
    /// </remarks>
    ReasoningSummary? IReasoningLlm.ReasonSummary => null;

    /// <inheritdoc />
    /// <remarks>grok-4.5 does not expose a verbosity knob.</remarks>
    Verbosity? IReasoningLlm.OutputVerbosity => null;

    /// <inheritdoc />
    public override string Name => "grok-4.5";

    /// <inheritdoc />
    public override decimal PriceInput => 2.00m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.30m;

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
    /// $6.00 below the threshold, $12.00 from 200K prompt tokens up — keyed on
    /// the <i>prompt</i> size, not the answer length.
    /// </summary>
    public override decimal GetOutputPrice(long inputTokens, long outputTokens)
        => inputTokens >= LongContextThreshold ? PriceOutput * LongContextMultiplier : PriceOutput;

    /// <summary>
    /// $0.30 below the threshold, $0.60 from 200K prompt tokens up.
    /// </summary>
    public override decimal GetCachedInputPrice(long inputTokens)
        => inputTokens >= LongContextThreshold
            ? PriceCachedInputValue * LongContextMultiplier
            : PriceCachedInputValue;
}

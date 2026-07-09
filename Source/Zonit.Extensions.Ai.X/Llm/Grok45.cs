namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.5 — xAI's smartest and fastest model, recommended for coding,
/// agents, engineering and general knowledge work. Frontier reasoning at a
/// smaller context window than <see cref="Grok43"/> but higher intelligence.
/// </summary>
/// <remarks>
/// <para>
/// Pricing: $2.00 / $6.00 per 1M tokens, $0.50 cached input. 500K context window.
/// </para>
/// <para>
/// Supports the <c>reasoning.effort</c> parameter ∈ { <c>low</c>, <c>medium</c>,
/// <c>high</c> }; xAI defaults to <c>high</c> when omitted. Set <see cref="Reason"/>
/// to override. Unlike <see cref="Grok43"/>, grok-4.5 does not accept
/// <see cref="ReasoningEffort.None"/> — leave <see cref="Reason"/> null to take the
/// <c>high</c> default. Reasoning summaries are emitted by xAI automatically — no
/// client-side toggle is required.
/// </para>
/// <para>
/// See <see href="https://docs.x.ai/developers/model-capabilities/text/reasoning"/>
/// and <see href="https://docs.x.ai/developers/grok-4-5"/>.
/// </para>
/// </remarks>
public class Grok45 : XChatBase, IReasoningLlm
{
    /// <summary>
    /// Thinking effort. <c>null</c> lets xAI pick the default (<c>high</c>).
    /// grok-4.5 accepts only <c>low</c>, <c>medium</c> and <c>high</c> —
    /// <see cref="ReasoningEffort.None"/> is rejected by the API.
    /// </summary>
    public ReasoningEffort? Reason { get; init; }

    /// <inheritdoc />
    ReasoningEffort? IReasoningLlm.Reason => Reason;

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
}

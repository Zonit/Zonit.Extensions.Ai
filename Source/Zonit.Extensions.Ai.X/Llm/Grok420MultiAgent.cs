namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Grok 4.20 Multi-Agent — Multi-agent variant of Grok 4.20 with parallel
/// agent coordination. Unique to this model: the <see cref="Agents"/>
/// property selects how many agents collaborate on the request (it is not
/// reasoning depth, despite riding on the same wire field).
/// </summary>
/// <remarks>
/// <para>
/// Pricing: $1.25 / $2.50 per 1M tokens, $0.3125 cached input.
/// Higher-context pricing applies above 200K tokens.
/// </para>
/// <para>
/// <b>Beta-access caveat:</b> the multi-agent endpoint rejects <i>client-side</i>
/// tools (custom <see cref="FunctionTool"/> entries from MCP, etc.) with
/// <c>"Client-side tools for multi-agent models require beta access"</c>
/// unless your xAI account is enrolled. Our agent session strips custom
/// function tools before sending the request — built-in tools like
/// <c>WebSearchTool</c> remain available because they execute
/// server-side.
/// </para>
/// </remarks>
[Obsolete("Superseded by Grok45 (grok-4.5), xAI's current flagship. Still works — upgrade for better quality.")]
public class Grok420MultiAgent : XChatBase
{
    /// <inheritdoc />
    public override string Name => "grok-4.20-multi-agent-0309";

    /// <inheritdoc />
    public override decimal PriceInput => 1.25m;

    /// <inheritdoc />
    public override decimal PriceCachedInputValue => 0.3125m;

    /// <inheritdoc />
    public override decimal PriceOutput => 2.50m;

    /// <summary>
    /// Number of agents that collaborate on the request. Maps 1:1 to the
    /// <c>reasoning.effort</c> wire field, but the docs are explicit that
    /// for this model the value selects agent count, not thinking effort.
    /// Leaving this <c>null</c> lets xAI pick the default (currently
    /// <see cref="AgentCount.Low"/>).
    /// </summary>
    public AgentCount? Agents { get; init; }

    /// <summary>
    /// Number of collaborating agents for grok-4.20-multi-agent. More agents
    /// produce deeper research at the cost of latency and tokens.
    /// </summary>
    public enum AgentCount
    {
        /// <summary>Fewest agents, fastest response.</summary>
        Low,
        /// <summary>Balanced agent count.</summary>
        Medium,
        /// <summary>More agents, deeper research.</summary>
        High,
        /// <summary>Maximum agents available; highest latency and cost.</summary>
        XHigh,
    }

    /// <inheritdoc />
    public override int MaxInputTokens => 2_000_000;

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

    /// <inheritdoc />
    public override decimal GetInputPrice(long tokenCount)
    {
        return tokenCount > 200_000 ? PriceInput * 2 : PriceInput;
    }

    /// <inheritdoc />
    public override decimal GetOutputPrice(long tokenCount)
    {
        return tokenCount > 200_000 ? PriceOutput * 2 : PriceOutput;
    }
}

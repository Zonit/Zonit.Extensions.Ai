namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-6 Cyber — the cybersecurity member of the GPT-6 generation, for approved
/// defenders doing authorized vulnerability research, exploit validation and
/// security testing. Successor to <see cref="Cyber56"/>.
/// </summary>
/// <remarks>
/// <para>
/// Model id <c>gpt-6-cyber</c>. 1.05M token context window (922K max input),
/// 128K max output.
/// </para>
/// <para>
/// <b>Gated model:</b> requires Daybreak Red access and approval — an ordinary
/// API key returns a model-not-found error, and custom rate limits may apply.
/// Chat Completions and Responses only; Batch is not supported.
/// </para>
/// <para>
/// Standard pricing ($20 input / $2 cached / $25 cache write / $100 output per 1M)
/// applies up to 272K input tokens, beyond which the long-context rates apply ($40 /
/// $4 / $50 / $150).
/// </para>
/// <para>
/// OpenAI does not list the accepted reasoning efforts; the class follows
/// <see cref="Astra6"/> (low … max, no <c>none</c>), so a level the model does not
/// take fails at the API rather than silently running without reasoning.
/// </para>
/// </remarks>
public class Cyber6 : OpenAiReasoningBase<OpenAiReasonEffortAlwaysOn>, IAgentLlm
{
    /// <inheritdoc />
    public override string Name => "gpt-6-cyber";

    /// <inheritdoc />
    public override decimal PriceInput => 20.00m;

    /// <inheritdoc />
    public override decimal PriceOutput => 100.00m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 2.00m;

    /// <inheritdoc />
    /// <remarks>1.25× the uncached input rate (30-minute retention); doubles with it past 272K input tokens.</remarks>
    public override decimal? PriceCachedInputWrite => 25.00m;

    /// <inheritdoc />
    public override int MaxInputTokens => 1_050_000;

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
        EndpointsType.Response;

    /// <summary>Context size past which OpenAI bills the whole request at the long-context rates.</summary>
    private const long LongContextThreshold = 272_000;

    /// <summary>Input-side rates (input, cache read, cache write) double past the threshold.</summary>
    private const decimal LongContextInputMultiplier = 2m;

    /// <summary>Output rate rises by half past the threshold ($100 → $150).</summary>
    private const decimal LongContextOutputMultiplier = 1.5m;

    /// <inheritdoc />
    public override decimal GetInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold ? PriceInput * LongContextInputMultiplier : PriceInput;

    /// <inheritdoc />
    public override decimal GetOutputPrice(long inputTokens, long outputTokens)
        => inputTokens > LongContextThreshold ? PriceOutput * LongContextOutputMultiplier : PriceOutput;

    /// <inheritdoc />
    public override decimal GetCachedInputPrice(long inputTokens)
        => inputTokens > LongContextThreshold
            ? PriceCachedInput!.Value * LongContextInputMultiplier
            : PriceCachedInput!.Value;
}

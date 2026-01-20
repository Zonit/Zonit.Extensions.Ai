namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// GPT-4o Mini - Fast, affordable small model for focused tasks.
/// </summary>
public class GPT4oMini : OpenAiChatBase
{
    /// <inheritdoc />
    public override string Name => "gpt-4o-mini";

    /// <inheritdoc />
    public override decimal PriceInput => 0.15m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.60m;

    /// <inheritdoc />
    public override decimal? PriceCachedInput => 0.075m;

    /// <inheritdoc />
    public override int MaxInputTokens => 128_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 16_384;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text | ChannelType.Image | ChannelType.Audio;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text | ChannelType.Audio;

    /// <inheritdoc />
    public override ToolsType SupportedTools =>
        ToolsType.FileSearch |
        ToolsType.CodeInterpreter;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling |
        FeaturesType.StructuredOutputs |
        FeaturesType.FineTuning;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints =>
        EndpointsType.Chat |
        EndpointsType.Response |
        EndpointsType.Assistant |
        EndpointsType.Batch;
}

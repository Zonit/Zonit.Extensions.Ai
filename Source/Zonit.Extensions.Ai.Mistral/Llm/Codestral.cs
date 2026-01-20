namespace Zonit.Extensions.Ai.Mistral;

/// <summary>
/// Codestral - Mistral's coding-optimized model.
/// State-of-the-art code generation.
/// </summary>
public class Codestral : MistralBase
{
    /// <inheritdoc />
    public override string Name => "codestral-latest";

    /// <inheritdoc />
    public override decimal PriceInput => 0.30m;

    /// <inheritdoc />
    public override decimal PriceOutput => 0.90m;

    /// <inheritdoc />
    public override int MaxInputTokens => 256_000;

    /// <inheritdoc />
    public override int MaxOutputTokens => 8_192;

    /// <inheritdoc />
    public override ChannelType Input => ChannelType.Text;

    /// <inheritdoc />
    public override ChannelType Output => ChannelType.Text;

    /// <inheritdoc />
    public override ToolsType SupportedTools => ToolsType.None;

    /// <inheritdoc />
    public override FeaturesType SupportedFeatures =>
        FeaturesType.Streaming |
        FeaturesType.FunctionCalling;

    /// <inheritdoc />
    public override EndpointsType SupportedEndpoints => EndpointsType.Chat;
}

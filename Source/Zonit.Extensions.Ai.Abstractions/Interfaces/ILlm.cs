namespace Zonit.Extensions.Ai;

/// <summary>
/// Base interface for all AI language models (LLM).
/// LLM contains model configuration - name, tokens, tools, pricing, capabilities.
/// </summary>
public interface ILlm
{
    /// <summary>
    /// The model identifier used by the API (e.g., "gpt-4.1", "claude-4-opus").
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Maximum tokens for generation output (user-configurable limit).
    /// </summary>
    int MaxTokens { get; }
    
    /// <summary>
    /// Price per 1M input tokens.
    /// </summary>
    decimal PriceInput { get; }
    
    /// <summary>
    /// Price per 1M output tokens.
    /// </summary>
    decimal PriceOutput { get; }
    
    /// <summary>
    /// Price per 1M batch input tokens (if supported).
    /// </summary>
    decimal? BatchPriceInput { get; }
    
    /// <summary>
    /// Price per 1M batch output tokens (if supported).
    /// </summary>
    decimal? BatchPriceOutput { get; }
    
    /// <summary>
    /// Maximum context window size in tokens.
    /// </summary>
    int MaxInputTokens { get; }
    
    /// <summary>
    /// Maximum output tokens the model can generate.
    /// </summary>
    int MaxOutputTokens { get; }
    
    /// <summary>
    /// Supported input modalities (text, image, audio).
    /// </summary>
    ChannelType Input { get; }
    
    /// <summary>
    /// Supported output modalities (text, image, audio).
    /// </summary>
    ChannelType Output { get; }
    
    /// <summary>
    /// Tools supported by this model.
    /// </summary>
    ToolsType SupportedTools { get; }
    
    /// <summary>
    /// Features supported by this model.
    /// </summary>
    FeaturesType SupportedFeatures { get; }
    
    /// <summary>
    /// API endpoints supported by this model.
    /// </summary>
    EndpointsType SupportedEndpoints { get; }
    
    /// <summary>
    /// Price per 1M input tokens for a request of this context size.
    /// </summary>
    /// <param name="inputTokens">
    /// Total input (context) tokens of the request — the size that triggers a
    /// provider's long-context tier (e.g. OpenAI &gt; 272K, xAI &gt; 128K).
    /// </param>
    decimal GetInputPrice(long inputTokens);

    /// <summary>
    /// Price per 1M output tokens for a request of this context size.
    /// </summary>
    /// <param name="inputTokens">
    /// Total input (context) tokens of the request. Long-context surcharges are
    /// keyed on the INPUT size even when they raise the output rate — e.g. OpenAI
    /// GPT-5.6 bills output at 1.5× once input exceeds 272K tokens.
    /// </param>
    /// <param name="outputTokens">Generated output tokens (reasoning included).</param>
    decimal GetOutputPrice(long inputTokens, long outputTokens);

    /// <summary>
    /// Price per 1M cache-read tokens for a request of this context size.
    /// Long-context tiers surcharge cache reads too (OpenAI GPT-5.6: 2× beyond
    /// 272K input tokens), so the rate cannot be a flat property.
    /// </summary>
    /// <remarks>
    /// Lives on <see cref="ILlm"/> rather than on <see cref="ITextLlm"/> because
    /// <see cref="IReasoningLlm"/> declares its own <c>PriceCachedInput</c> without
    /// deriving from <see cref="ITextLlm"/> — gating cache pricing on the marker
    /// interface silently billed every reasoning model's cache reads at the full
    /// input rate.
    /// </remarks>
    /// <param name="inputTokens">Total input (context) tokens of the request.</param>
    decimal GetCachedInputPrice(long inputTokens);

    /// <summary>
    /// Price per 1M cache-write tokens for a request of this context size.
    /// Returns the base input rate for providers that do not bill cache writes.
    /// </summary>
    /// <param name="inputTokens">Total input (context) tokens of the request.</param>
    decimal GetCachedInputWritePrice(long inputTokens);

    /// <summary>
    /// Price per 1M input tokens on the batch endpoint (typically 0.5× standard).
    /// Falls back to <see cref="BatchPriceInput"/>, then to half of
    /// <see cref="GetInputPrice"/>.
    /// </summary>
    /// <param name="inputTokens">Total input (context) tokens of the request.</param>
    decimal GetBatchInputPrice(long inputTokens);

    /// <summary>
    /// Price per 1M output tokens on the batch endpoint (typically 0.5× standard).
    /// Falls back to <see cref="BatchPriceOutput"/>, then to half of
    /// <see cref="GetOutputPrice"/>.
    /// </summary>
    /// <param name="inputTokens">Total input (context) tokens of the request.</param>
    /// <param name="outputTokens">Generated output tokens.</param>
    decimal GetBatchOutputPrice(long inputTokens, long outputTokens);

    /// <summary>
    /// Tools configured for this model instance (function calling, web search, etc.).
    /// Tools belong to the model, not the prompt!
    /// </summary>
    IToolBase[]? Tools { get; }
}

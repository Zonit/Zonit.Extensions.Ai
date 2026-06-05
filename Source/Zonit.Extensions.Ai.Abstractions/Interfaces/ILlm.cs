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
    /// Calculates input price based on token count (may vary with context size).
    /// </summary>
    decimal GetInputPrice(long tokenCount);
    
    /// <summary>
    /// Calculates output price based on token count.
    /// </summary>
    decimal GetOutputPrice(long tokenCount);
    
    /// <summary>
    /// Tools configured for this model instance (function calling, web search, etc.).
    /// Tools belong to the model, not the prompt!
    /// </summary>
    IToolBase[]? Tools { get; }
}

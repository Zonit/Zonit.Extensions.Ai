namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that supports text generation with temperature/sampling control.
/// </summary>
public interface ITextLlm : ILlm
{
    /// <summary>
    /// Price per 1M cached input tokens read from cache (if supported).
    /// Typically 0.1x base input price.
    /// </summary>
    decimal? PriceCachedInput { get; }

    /// <summary>
    /// Price per 1M tokens written to cache (if supported).
    /// Typically 1.25x base input price (Anthropic 5-min TTL) or 2x (Anthropic 1-hour TTL).
    /// Null means cache writes are not supported or are billed at the base input rate.
    /// </summary>
    decimal? PriceCachedInputWrite => null;
    
    /// <summary>
    /// Temperature for response randomness (0.0 - 2.0).
    /// </summary>
    double Temperature { get; set; }
    
    /// <summary>
    /// Top-p nucleus sampling (0.0 - 1.0).
    /// </summary>
    double TopP { get; set; }
}

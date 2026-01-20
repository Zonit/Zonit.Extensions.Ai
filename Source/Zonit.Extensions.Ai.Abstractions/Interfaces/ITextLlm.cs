namespace Zonit.Extensions.Ai;

/// <summary>
/// LLM that supports text generation with temperature/sampling control.
/// </summary>
public interface ITextLlm : ILlm
{
    /// <summary>
    /// Price per 1M cached input tokens (if supported).
    /// </summary>
    decimal? PriceCachedInput { get; }
    
    /// <summary>
    /// Temperature for response randomness (0.0 - 2.0).
    /// </summary>
    double Temperature { get; set; }
    
    /// <summary>
    /// Top-p nucleus sampling (0.0 - 1.0).
    /// </summary>
    double TopP { get; set; }
}

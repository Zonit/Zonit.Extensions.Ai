namespace Zonit.Extensions.Ai.Llm;

public abstract class OpenAiReasoningBase : OpenAiBase, ITextLlmBase
{
    public virtual decimal? PriceCachedInput { get; } = null;

    /// <summary>
    /// Controls the reasoning depth for reasoning models (o-series, GPT-5).
    /// Higher effort results in deeper reasoning, more tokens, and potentially better accuracy.
    /// GPT-5.1 defaults to None if not specified.
    /// </summary>
    public virtual ReasonType? Reason { get; init; }
    
    /// <summary>
    /// Controls whether and how the model's reasoning summary is returned.
    /// Only available with Responses API endpoint.
    /// </summary>
    public virtual ReasonSummaryType? ReasonSummary { get; init; }
    
    /// <summary>
    /// Controls the verbosity of model output (GPT-5 series only).
    /// Provides granular control over response length and detail.
    /// </summary>
    public virtual VerbosityType? Verbosity { get; init; }

    /// <summary>
    /// Reasoning effort levels for reasoning models.
    /// Note: GPT-5 models do NOT support temperature, top_p, or logprobs parameters.
    /// </summary>
    public enum ReasonType
    {
        /// <summary>
        /// No reasoning effort (GPT-5.1 default). Fastest response with minimal processing.
        /// </summary>
        None,
        
        ///// <summary>
        ///// Minimal reasoning with few internal tokens. Optimized for throughput.
        ///// Note: Parallel tool calls are not supported at this level.
        ///// </summary>
        //Minimal,
        
        /// <summary>
        /// Light reasoning with quick judgment. Fast response with moderate accuracy.
        /// </summary>
        Low,
        
        /// <summary>
        /// Balanced depth vs speed. Safe general-purpose choice (default for most models).
        /// </summary>
        Medium,
        
        /// <summary>
        /// Deep, multistep reasoning for complex problems. Slowest but highest accuracy.
        /// </summary>
        High
    }
    
    /// <summary>
    /// Reasoning summary options for Responses API.
    /// </summary>
    public enum ReasonSummaryType
    {
        None,
        Auto,
        Detailed,
    }
    
    /// <summary>
    /// Output verbosity control for GPT-5 models.
    /// Replaces temperature/top_p for response length control.
    /// </summary>
    public enum VerbosityType
    {
        /// <summary>
        /// Concise responses with minimal elaboration.
        /// </summary>
        Low,
        
        /// <summary>
        /// Balanced detail and brevity (default).
        /// </summary>
        Medium,
        
        /// <summary>
        /// Detailed, comprehensive responses with full elaboration.
        /// </summary>
        High
    }
}

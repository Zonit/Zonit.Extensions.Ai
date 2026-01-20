using System.ComponentModel;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Response for sentiment analysis.
/// </summary>
public class SentimentResponse
{
    /// <summary>
    /// Overall sentiment: Positive, Negative, Neutral, or Mixed.
    /// </summary>
    [Description("Overall sentiment: Positive, Negative, Neutral, or Mixed")]
    public required string Sentiment { get; set; }
    
    /// <summary>
    /// Confidence score (0-1).
    /// </summary>
    [Description("Confidence score from 0.0 to 1.0")]
    public double Confidence { get; set; }
    
    /// <summary>
    /// Detected emotions.
    /// </summary>
    [Description("List of detected emotions (e.g., joy, anger, fear, sadness)")]
    public List<string>? Emotions { get; set; }
    
    /// <summary>
    /// Brief explanation of the sentiment.
    /// </summary>
    [Description("Brief explanation of why this sentiment was detected")]
    public string? Explanation { get; set; }
}

/// <summary>
/// Prompt for analyzing sentiment of text.
/// </summary>
/// <example>
/// var result = await ai.GenerateAsync(
///     new GPT51(),
///     new SentimentPrompt { Content = "I love this product!" });
/// Console.WriteLine(result.Value.Sentiment); // "Positive"
/// </example>
public class SentimentPrompt : PromptBase<SentimentResponse>
{
    /// <summary>
    /// Text to analyze.
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Include emotion detection.
    /// </summary>
    public bool IncludeEmotions { get; init; } = true;
    
    /// <inheritdoc />
    public override string Prompt => @"
Analyze the sentiment of the following text.
Provide the overall sentiment (Positive, Negative, Neutral, or Mixed) and confidence score.
{{~ if include_emotions ~}}
Also identify any specific emotions present in the text.
{{~ end ~}}
Provide a brief explanation of your analysis.

Text to analyze:
{{ content }}
";
}

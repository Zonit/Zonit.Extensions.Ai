using System.ComponentModel;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Response for text summarization.
/// </summary>
public class SummarizeResponse
{
    /// <summary>
    /// Brief summary of the text.
    /// </summary>
    [Description("Brief summary of the input text")]
    public required string Summary { get; set; }

    /// <summary>
    /// Key points extracted from the text.
    /// </summary>
    [Description("List of key points from the text")]
    public List<string>? KeyPoints { get; set; }

    /// <summary>
    /// Estimated reading time in minutes for original text.
    /// </summary>
    [Description("Estimated reading time of the original text in minutes")]
    public int? OriginalReadingTimeMinutes { get; set; }
}

/// <summary>
/// Prompt for summarizing text content.
/// </summary>
/// <example>
/// var result = await ai.GenerateAsync(
///     new GPT51(),
///     new SummarizePrompt { Content = longArticle, MaxWords = 100 });
/// Console.WriteLine(result.Value.Summary);
/// </example>
public class SummarizePrompt : PromptBase<SummarizeResponse>
{
    /// <summary>
    /// Text to summarize.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Maximum words in summary (default: 150).
    /// </summary>
    public int MaxWords { get; init; } = 150;

    /// <summary>
    /// Include key points extraction.
    /// </summary>
    public bool IncludeKeyPoints { get; init; } = true;

    /// <inheritdoc />
    public override string Prompt => @"
Summarize the following text in {{ max_words }} words or less.
{{~ if include_key_points ~}}
Also extract 3-5 key points from the text.
{{~ end ~}}

Text to summarize:
{{ content }}
";
}

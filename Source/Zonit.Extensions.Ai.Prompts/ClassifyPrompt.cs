using System.ComponentModel;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Response for classification prompt.
/// </summary>
public class ClassifyResponse
{
    /// <summary>
    /// Selected category.
    /// </summary>
    [Description("The selected category from the provided options")]
    public required string Category { get; set; }

    /// <summary>
    /// Confidence score (0-1).
    /// </summary>
    [Description("Classification confidence score from 0.0 to 1.0")]
    public double Confidence { get; set; }

    /// <summary>
    /// Reasoning for the classification.
    /// </summary>
    [Description("Brief explanation of why this category was chosen")]
    public string? Reasoning { get; set; }

    /// <summary>
    /// Alternative categories with scores.
    /// </summary>
    [Description("Other possible categories with their confidence scores")]
    public Dictionary<string, double>? Alternatives { get; set; }
}

/// <summary>
/// Prompt for text classification into categories.
/// </summary>
/// <example>
/// var result = await ai.GenerateAsync(
///     new GPT51(),
///     new ClassifyPrompt 
///     { 
///         Content = "My order hasn't arrived yet",
///         Categories = ["Shipping", "Billing", "Technical", "General"]
///     });
/// Console.WriteLine(result.Value.Category); // "Shipping"
/// </example>
public class ClassifyPrompt : PromptBase<ClassifyResponse>
{
    /// <summary>
    /// Text to classify.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Available categories.
    /// </summary>
    public required List<string> Categories { get; init; }

    /// <summary>
    /// Include alternative categories with scores.
    /// </summary>
    public bool IncludeAlternatives { get; init; } = false;

    /// <inheritdoc />
    public override string Prompt => @"
Classify the following text into one of these categories:
{{~ for cat in categories ~}}
- {{ cat }}
{{~ end ~}}

Text: {{ content }}

Provide the best matching category with a confidence score.
{{~ if include_alternatives ~}}
Also provide scores for other possible categories.
{{~ end ~}}
";
}

/// <summary>
/// Multi-label classification response.
/// </summary>
public class MultiLabelResponse
{
    /// <summary>
    /// Selected labels with confidence scores.
    /// </summary>
    [Description("Selected labels with their confidence scores")]
    public required Dictionary<string, double> Labels { get; set; }

    /// <summary>
    /// Reasoning for the selection.
    /// </summary>
    [Description("Brief explanation of the label selection")]
    public string? Reasoning { get; set; }
}

/// <summary>
/// Prompt for multi-label classification (multiple categories can apply).
/// </summary>
public class MultiLabelPrompt : PromptBase<MultiLabelResponse>
{
    /// <summary>
    /// Text to classify.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Available labels.
    /// </summary>
    public required List<string> Labels { get; init; }

    /// <summary>
    /// Minimum confidence threshold for a label to be included.
    /// </summary>
    public double MinConfidence { get; init; } = 0.5;

    /// <inheritdoc />
    public override string Prompt => @"
Classify the following text with ALL applicable labels from this list:
{{~ for label in labels ~}}
- {{ label }}
{{~ end ~}}

Only include labels with confidence >= {{ min_confidence }}.
Text: {{ content }}
";
}

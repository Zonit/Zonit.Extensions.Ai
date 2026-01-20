using System.ComponentModel;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Response for question and answer prompts.
/// </summary>
public class AnswerResponse
{
    /// <summary>
    /// The answer to the question.
    /// </summary>
    [Description("The answer to the question")]
    public required string Answer { get; set; }
    
    /// <summary>
    /// Confidence score (0-1).
    /// </summary>
    [Description("Confidence score from 0.0 to 1.0")]
    public double Confidence { get; set; }
    
    /// <summary>
    /// Sources or references used.
    /// </summary>
    [Description("Sources or references that support the answer")]
    public List<string>? Sources { get; set; }
    
    /// <summary>
    /// Whether the answer is based on the provided context or general knowledge.
    /// </summary>
    [Description("True if answer is from provided context, false if from general knowledge")]
    public bool FromContext { get; set; }
}

/// <summary>
/// Prompt for question answering with context.
/// </summary>
/// <example>
/// var result = await ai.GenerateAsync(
///     new GPT51(),
///     new QuestionPrompt 
///     { 
///         Question = "What is the capital of France?",
///         Context = "France is a country in Europe. Its capital is Paris."
///     });
/// Console.WriteLine(result.Value.Answer); // "Paris"
/// </example>
public class QuestionPrompt : PromptBase<AnswerResponse>
{
    /// <summary>
    /// The question to answer.
    /// </summary>
    public required string Question { get; init; }
    
    /// <summary>
    /// Optional context to base the answer on.
    /// </summary>
    public string? Context { get; init; }
    
    /// <summary>
    /// Whether to restrict answers to provided context only.
    /// </summary>
    public bool StrictContext { get; init; } = false;
    
    /// <inheritdoc />
    public override string Prompt => @"
{{~ if context ~}}
Based on the following context:
{{ context }}

{{~ if strict_context ~}}
Answer ONLY using information from the context above. If the answer is not in the context, say ""I cannot answer based on the provided context.""
{{~ end ~}}
{{~ end ~}}

Question: {{ question }}

Provide a clear, concise answer with a confidence score.
";
}

using System.ComponentModel;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Response for code generation.
/// </summary>
public class CodeResponse
{
    /// <summary>
    /// Generated code.
    /// </summary>
    [Description("The generated code")]
    public required string Code { get; set; }

    /// <summary>
    /// Programming language used.
    /// </summary>
    [Description("Programming language of the generated code")]
    public required string Language { get; set; }

    /// <summary>
    /// Explanation of the code.
    /// </summary>
    [Description("Brief explanation of what the code does")]
    public string? Explanation { get; set; }

    /// <summary>
    /// Usage example.
    /// </summary>
    [Description("Example of how to use the generated code")]
    public string? UsageExample { get; set; }
}

/// <summary>
/// Prompt for generating code.
/// </summary>
/// <example>
/// var result = await ai.GenerateAsync(
///     new GPT51(),
///     new CodePrompt 
///     { 
///         Description = "Function to calculate Fibonacci sequence", 
///         Language = "C#" 
///     });
/// Console.WriteLine(result.Value.Code);
/// </example>
public class CodePrompt : PromptBase<CodeResponse>
{
    /// <summary>
    /// Description of the code to generate.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Target programming language.
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Include code explanation.
    /// </summary>
    public bool IncludeExplanation { get; init; } = true;

    /// <summary>
    /// Include usage example.
    /// </summary>
    public bool IncludeExample { get; init; } = true;

    /// <inheritdoc />
    public override string Prompt => @"
Generate {{ language }} code based on the following description:
{{ description }}

{{~ if include_explanation ~}}
Include a brief explanation of how the code works.
{{~ end ~}}
{{~ if include_example ~}}
Include a usage example.
{{~ end ~}}

Provide clean, well-documented, production-ready code.
";
}

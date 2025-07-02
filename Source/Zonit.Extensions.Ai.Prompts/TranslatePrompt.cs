using Zonit.Extensions.Ai.Abstractions;

namespace Zonit.Extensions.Ai.Prompts;

public class TranslatePrompt : PromptBase<string>
{
    public required string Content { get; set; }
    public required string Language { get; set; }
    public string? Culture { get; set; }

    public override string Prompt => @"
Your task is to translate the following text into ``{{ language }}``
{{~ if culture ~}}
for ``{{ culture }}`` culture
{{~ end ~}}

Translation guidelines:
- Preserve the original meaning and tone of the text.
- Use natural, fluent language appropriate for the target audience.
- Consider cultural context and adapt idioms, expressions, and references appropriately.
- Do not add your own comments or explanations.
- If the text contains technical terms, maintain their accurate meaning.
- Return only the translated text without additional annotations.

Text to translate:
``{{ content }}``
";
}
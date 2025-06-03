using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Prompts;

namespace Example.Prompts;

internal class TranslatePrompt : PromptBase<string>
{
    [PromptKey]
    public required string Content { get; init; }

    [PromptKey]
    public required string Culture { get; init; }

    public override string Prompt => "Translate the content of {{ content }} to {{ culture }}";
}
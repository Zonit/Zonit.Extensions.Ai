using Zonit.Extensions.Ai.Prompts;

namespace Example.Prompts;

internal class TranslatePrompt : PromptBase<string>
{
    public required string Content { get; init; }
    public required string Culture { get; init; }

    public override string Prompt => "Translate the content of ``{{~ content ~}}`` to ``{{~ culture ~}}``";
}
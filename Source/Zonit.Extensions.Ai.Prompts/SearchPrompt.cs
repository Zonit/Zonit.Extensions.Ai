using Zonit.Extensions.Ai.Abstractions;

namespace Zonit.Extensions.Ai.Prompts;

public class SearchPrompt : PromptBase<string>
{
    public required string Query { get; init; }

    public override IReadOnlyList<ITool> Tools { get; } = [
        new WebSearchTool{ ContextSize = WebSearchTool.ContextSizeType.High },
    ];

    public override string Prompt => @"Twoim zadaniem jest wyszukanie wszystkich informacji na temat ``{{ query }}``";
}
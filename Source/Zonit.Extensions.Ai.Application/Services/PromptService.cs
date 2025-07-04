using Scriban;
using Scriban.Runtime;
using Zonit.Extensions.Ai;

public static class PromptService
{
    private static readonly HashSet<string> BlockedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(IPromptBase.Tools),
        nameof(IPromptBase.ToolChoice),
        nameof(IPromptBase.UserName),
        "ModelType"
    };

    public static string BuildPrompt(IPromptBase prompt)
    {
        var template = Template.Parse(prompt.Prompt);

        if (template.HasErrors)
        {
            var errorMessage = string.Join("\n", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Scriban template parse error:\n{errorMessage}");
        }

        // Tworzymy kontekst z domyślnym renamerem (PascalCase -> snake_case)
        var context = new TemplateContext
        {
            MemberRenamer = member => member.Name
        };

        // Używamy wbudowanej funkcji Import która automatycznie konwertuje właściwości
        var scriptObject = new ScriptObject();
        scriptObject.Import(prompt, renamer: member =>
        {
            // Pomijamy zablokowane właściwości
            if (BlockedProperties.Contains(member.Name))
                return null;

            // Używamy wbudowanego renameru Scribana
            return StandardMemberRenamer.Default(member);
        });

        context.PushGlobal(scriptObject);

        return template.Render(context).Trim();
    }
}
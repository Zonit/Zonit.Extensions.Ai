using Scriban;
using Scriban.Runtime;
using System.Reflection;
using System.Text.Json;

namespace Zonit.Extensions.Ai.Application.Services;

public static class PromptService
{
    // TODO: Dodaj takie typy jak np IFile do blokowania
    public static string BuildPrompt(IPromptBase prompt)
    {
        var template = Template.Parse(prompt.Prompt);

        var context = new TemplateContext();
        var scriptObject = new ScriptObject();

        // Lista nazw właściwości do zablokowania
        var blockedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(prompt.Tools),
            nameof(prompt.ToolChoice),
            nameof(prompt.UserName),
            "ModelType"
        };

        // Pobierz wszystkie publiczne właściwości z obiektu prompt
        var properties = prompt.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (!blockedProperties.Contains(prop.Name))
            {
                var snakeCaseName = JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name);
                scriptObject[snakeCaseName] = prop.GetValue(prompt);
            }
        }

        context.PushGlobal(scriptObject);

        return template.Render(context);
    }
}
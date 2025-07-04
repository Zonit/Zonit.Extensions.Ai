using Scriban;
using Scriban.Runtime;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Zonit.Extensions.Ai.Application.Services;

public static class PromptService
{
    // TODO: Dodaj takie typy jak np IFile do blokowania
    public static string BuildPrompt(IPromptBase prompt)
    {
        var template = Template.Parse(prompt.Prompt);

        if (template.HasErrors)
        {
            var errorMessage = string.Join("\n", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Scriban template parse error:\n{errorMessage}");
        }

        var context = new TemplateContext();
        var scriptObject = new ScriptObject();

        var blockedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(prompt.Tools),
            nameof(prompt.ToolChoice),
            nameof(prompt.UserName),
            "ModelType"
        };

        var properties = prompt.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (!blockedProperties.Contains(prop.Name))
            {
                var snakeCaseName = JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name);
                var value = prop.GetValue(prompt);

                // Jeżeli wartość to null i to kolekcja, podstaw pustą
                if (value == null)
                {
                    if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                    {
                        value = Activator.CreateInstance(prop.PropertyType);
                    }
                    else
                    {
                        continue; // pomiń null jeśli nie kolekcja
                    }
                }

                scriptObject[snakeCaseName] = value;
            }
        }

        context.PushGlobal(scriptObject);

        return template.Render(context);
    }

}
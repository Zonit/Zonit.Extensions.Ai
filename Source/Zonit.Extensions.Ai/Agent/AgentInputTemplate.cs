using System.Text.Json;
using Scriban;
using Scriban.Runtime;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Renders a parametrized sub-agent's Scriban <c>Prompt</c> template against the arguments the parent
/// model supplied (validated against the agent's <c>TInput</c> schema). The JSON is mapped straight to
/// Scriban variables — keys to snake_case — so the agent author writes <c>{{ symbol }}</c> the same way
/// a <c>PromptBase</c> does. Fully reflection-free: <c>TInput</c> only exists to generate the schema,
/// while the model's JSON is the data at render time.
/// </summary>
internal static class AgentInputTemplate
{
    public static string Render(string promptTemplate, JsonElement arguments)
    {
        if (string.IsNullOrEmpty(promptTemplate))
            return string.Empty;

        var template = Template.Parse(promptTemplate);
        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Agent prompt template parse error: {string.Join(", ", template.Messages)}");

        var scriptObject = ToScriptObject(arguments);
        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        return template.Render(context);
    }

    private static ScriptObject ToScriptObject(JsonElement element)
    {
        var scriptObject = new ScriptObject();
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                scriptObject.Add(AiNaming.ToSnakeCase(property.Name), ToClr(property.Value));
        return scriptObject;
    }

    private static object? ToClr(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ToClr).ToList(),
        JsonValueKind.Object => ToScriptObject(element),
        _ => element.GetRawText(),
    };
}

using System.Reflection;
using Scriban;
using Scriban.Runtime;

namespace Zonit.Extensions.Ai.Prompts;

public abstract class PromptBase<TResponse> where TResponse : class
{
    public abstract string Prompt { get; }
    public Type ResponseType => typeof(TResponse);

    public virtual TResponse CreateResponseInstance()
    {
        // Próba utworzenia instancji z bezparametrowego konstruktora
        if (typeof(TResponse).GetConstructor(Type.EmptyTypes) != null)
        {
            return Activator.CreateInstance<TResponse>();
        }

        // Jeśli nie ma bezparametrowego konstruktora, zgłoś wyjątek
        throw new InvalidOperationException(
            $"Typ {typeof(TResponse).Name} nie posiada bezparametrowego konstruktora. " +
            "Nadpisz metodę CreateResponseInstance() w klasie dziedziczącej.");
    }

    public Dictionary<string, object> GetParameters()
    {
        var parameters = new Dictionary<string, object>();
        var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<PromptKeyAttribute>() != null);

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<PromptKeyAttribute>();
            var paramName = attribute?.Name ?? property.Name;
            var value = property.GetValue(this);
            parameters[paramName] = value ?? string.Empty;
        }

        return parameters;
    }

    public string BuildPrompt()
    {
        var templateContext = new TemplateContext
        {
            // Ignorowanie wielkości liter w nazwach zmiennych
            MemberRenamer = member => member.Name.ToLowerInvariant()
        };
        var scriptObject = new ScriptObject();

        var parameters = GetParameters();
        foreach (var param in parameters)
        {
            scriptObject.Add(param.Key.ToLowerInvariant(), param.Value);
        }

        templateContext.PushGlobal(scriptObject);

        var template = Template.Parse(Prompt);
        return template.Render(templateContext);
    }
}
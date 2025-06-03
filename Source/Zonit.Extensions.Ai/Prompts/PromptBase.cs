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

    //public Dictionary<string, object> GetParameters()
    //{
    //    var parameters = new Dictionary<string, object>();
    //    var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

    //    foreach (var property in properties)
    //    {
    //        var attribute = property.GetCustomAttribute<PromptKeyAttribute>();
    //        var paramName = attribute?.Name ?? property.Name;
    //        var value = property.GetValue(this);
    //        parameters[paramName] = value ?? string.Empty;
    //    }

    //    return parameters;
    //}

    //public string BuildPrompt2()
    //{
    //    if (Prompt == null)
    //        throw new InvalidOperationException("Prompt nie może być null.");

    //    var template = Template.Parse(Prompt);
    //    if (template.HasErrors)
    //        throw new InvalidOperationException($"Błąd parsowania szablonu: {string.Join(", ", template.Messages.Select(m => m.Message))}");

    //    var templateContext = new TemplateContext
    //    {
    //        EnableRelaxedMemberAccess = true,
    //        MemberRenamer = StandardMemberRenamer.Default
    //    };

    //    var scriptObject = new ScriptObject();
    //    scriptObject.Import(GetParameters());
    //    templateContext.PushGlobal(scriptObject);

    //    return template.Render(templateContext);
    //}

    public string BuildPrompt()
    {
        var templateContext = new TemplateContext();

        templateContext.CurrentGlobal.Import(
            this,
            ScriptMemberImportFlags.Field | ScriptMemberImportFlags.Property,
            null,
            StandardMemberRenamer.Default
        );

        var template = Template.Parse(Prompt);

        return template.Render(templateContext);
    }
}
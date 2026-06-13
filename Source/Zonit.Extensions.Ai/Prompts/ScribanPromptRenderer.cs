using System.Diagnostics.CodeAnalysis;
using Scriban;
using Scriban.Runtime;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Default <see cref="IPromptRenderer"/> implementation backed by Scriban.
/// </summary>
/// <remarks>
/// <para>
/// For typed prompts inheriting <see cref="PromptBase{TResponse}"/>, the renderer
/// parses the raw <c>Prompt</c> template and populates a Scriban <c>ScriptObject</c>
/// using the AOT-safe binding registered by the <c>AiPromptBindingGenerator</c> source
/// generator (via <see cref="PromptBindingRegistry"/>). When no binding is present
/// — e.g. the prompt was synthesised at runtime, defined in a domain assembly that
/// does not reference the source generator, or only metadata-visible to the consumer —
/// the renderer falls back to reflection-based property discovery (annotated with
/// <see cref="RequiresUnreferencedCodeAttribute"/>).
/// </para>
/// <para>
/// Prompts that are not <see cref="PromptBase{TResponse}"/> instances (e.g.
/// <see cref="SimplePrompt{TResponse}"/>) are returned by their <c>Text</c> as-is.
/// </para>
/// </remarks>
internal sealed class ScribanPromptRenderer : IPromptRenderer
{
    private static readonly string[] ExcludedPropertyNames = { "Prompt", "Text", "Files" };

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification =
            "PopulateScriptObjectViaReflection is the documented reflection-based fallback for prompt " +
            "types whose AOT-safe binding was not emitted by AiPromptBindingGenerator. The fallback " +
            "is gated by PromptBindingRegistry.TryPopulate returning false; under AOT/trimmed builds " +
            "the generator is always active and the fallback path is never taken.")]
    public string Render(IPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        // Non-PromptBase prompts (SimplePrompt, agent shims, etc.) carry their final
        // text in Text directly; nothing to template.
        if (prompt is not PromptBase templated)
            return prompt.Text;

        var raw = templated.Prompt;
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var template = Template.Parse(raw);
        if (template.HasErrors)
            throw new InvalidOperationException($"Template parse error: {string.Join(", ", template.Messages)}");

        var scriptObject = new ScriptObject();
        if (!PromptBindingRegistry.TryPopulate(prompt, scriptObject))
            PopulateScriptObjectViaReflection(prompt, scriptObject);

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        return template.Render(context);
    }

    [RequiresUnreferencedCode(
        "Reflection-based property discovery for Scriban templating. " +
        "Reference Zonit.Extensions.Ai's source generator (default for the package) " +
        "to get an AOT-safe binding emitted for this prompt type.")]
    private static void PopulateScriptObjectViaReflection(IPrompt prompt, ScriptObject scriptObject)
    {
        foreach (var prop in prompt.GetType().GetProperties())
        {
            if (Array.IndexOf(ExcludedPropertyNames, prop.Name) >= 0)
                continue;

            var value = prop.GetValue(prompt);
            var snakeName = AiNaming.ToSnakeCase(prop.Name);
            scriptObject.Add(snakeName, value);
        }
    }
}

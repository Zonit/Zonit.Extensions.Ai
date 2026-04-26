using System.Collections.Concurrent;
using Scriban.Runtime;

namespace Zonit.Extensions.Ai;

/// <summary>
/// AOT-safe registry of <see cref="ScriptObject"/> population delegates for
/// concrete <see cref="PromptBase{TResponse}"/> subclasses.
/// </summary>
/// <remarks>
/// <para>
/// Entries are produced by the <c>AiPromptBindingGenerator</c> source generator
/// at build time and inserted into the registry via a
/// <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/> in
/// the consumer assembly — i.e. the registry is fully populated before any
/// prompt is rendered, with **zero reflection** at runtime.
/// </para>
/// <para>
/// When a prompt type has no entry (e.g. it was defined via reflection-emit,
/// in a non-AOT consumer, or before the generator could see it),
/// <see cref="PromptBase{TResponse}"/> falls back to reflection-based
/// property discovery (annotated with
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/>).
/// </para>
/// </remarks>
public static class PromptBindingRegistry
{
    private static readonly ConcurrentDictionary<Type, Action<object, ScriptObject>> _bindings = new();

    /// <summary>
    /// Registers a binding that populates a <see cref="ScriptObject"/> from the
    /// given prompt type's properties. Called by source-generated module
    /// initializers — application code typically does not invoke this directly.
    /// </summary>
    public static void Register(Type promptType, Action<object, ScriptObject> binding)
    {
        ArgumentNullException.ThrowIfNull(promptType);
        ArgumentNullException.ThrowIfNull(binding);
        _bindings[promptType] = binding;
    }

    /// <summary>
    /// Tries to populate <paramref name="scriptObject"/> using a registered
    /// binding for <paramref name="prompt"/>'s runtime type.
    /// </summary>
    /// <returns>
    /// <c>true</c> when an AOT-safe binding handled the population;
    /// <c>false</c> when the caller should fall back to reflection.
    /// </returns>
    public static bool TryPopulate(object prompt, ScriptObject scriptObject)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(scriptObject);

        if (_bindings.TryGetValue(prompt.GetType(), out var binding))
        {
            binding(prompt, scriptObject);
            return true;
        }

        return false;
    }
}

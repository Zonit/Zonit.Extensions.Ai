using System.Collections.Concurrent;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Process-wide registry of tool types discovered by the
/// <c>AiToolRegistrationGenerator</c> source generator. The generator emits a
/// <c>[ModuleInitializer]</c> in every consumer assembly that calls
/// <see cref="Register(Type)"/> for each concrete <c>ToolBase&lt;,&gt;</c>
/// it finds — so by the time <c>AddAi()</c> runs, every tool type from every
/// loaded assembly is already announced here.
/// </summary>
/// <remarks>
/// This is the bridge that lets <c>AddAi()</c> register all project tools
/// automatically without the consumer calling a separate <c>AddAiTools()</c>.
/// Registration is idempotent and thread-safe.
/// </remarks>
public static class ToolDiscovery
{
    // ConcurrentDictionary used as a thread-safe set; values are unused.
    private static readonly ConcurrentDictionary<Type, byte> _types = new();

    /// <summary>
    /// Announces a <see cref="ITool"/> implementation type to be picked up by
    /// the next <c>AddAi()</c> call. Safe to call from a module initializer.
    /// </summary>
    /// <param name="toolType">A concrete (non-abstract, non-generic) <see cref="ITool"/> type.</param>
    public static void Register(Type toolType)
    {
        if (toolType is null) return;
        if (toolType.IsAbstract || toolType.IsGenericTypeDefinition) return;
        if (!typeof(ITool).IsAssignableFrom(toolType)) return;
        _types.TryAdd(toolType, 0);
    }

    /// <summary>
    /// Snapshot of all registered tool types — read by <c>AddAi()</c>.
    /// </summary>
    public static IReadOnlyCollection<Type> RegisteredTypes => _types.Keys.ToArray();
}

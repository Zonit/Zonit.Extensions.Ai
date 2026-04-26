using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Zonit.Extensions.Ai;

/// <summary>
/// AOT-safe <see cref="IJsonTypeInfoResolver"/> populated by source generators
/// at build time. Each registered factory builds a <see cref="JsonTypeInfo"/>
/// for a specific runtime type using <see cref="JsonMetadataServices"/> —
/// i.e. **no reflection** at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Registration happens through <see cref="Register"/>, called from
/// <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/>s
/// emitted by the <c>AiJsonTypeInfoGenerator</c>. When resolving a type that
/// has no registered factory (e.g. unsupported shape, or generator did not
/// see the type), <see cref="GetTypeInfo"/> returns <c>null</c> — the caller
/// then falls back to reflection-based serialisation
/// (annotated with <c>[RequiresUnreferencedCode]</c>).
/// </para>
/// <para>
/// The resolver caches built <see cref="JsonTypeInfo"/> instances per
/// <see cref="JsonSerializerOptions"/> instance to honour
/// <see cref="JsonMetadataServices"/>'s contract (an info instance is bound
/// to a single options instance).
/// </para>
/// </remarks>
public sealed class AiJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    /// <summary>Singleton — registrations are global to the process.</summary>
    public static AiJsonTypeInfoResolver Instance { get; } = new();

    private static readonly ConcurrentDictionary<Type, Func<JsonSerializerOptions, JsonTypeInfo>> _factories = new();

    // Cache: each options instance gets its own JsonTypeInfo (STJ contract).
    private readonly ConditionalWeakTable<JsonSerializerOptions, ConcurrentDictionary<Type, JsonTypeInfo>> _cache = new();

    private AiJsonTypeInfoResolver() { }

    /// <summary>
    /// Registers a factory that builds a <see cref="JsonTypeInfo"/> for
    /// <paramref name="type"/>. Called by source-generated module initializers.
    /// </summary>
    public static void Register(Type type, Func<JsonSerializerOptions, JsonTypeInfo> factory)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(factory);
        _factories[type] = factory;
    }

    /// <summary>
    /// Returns <c>true</c> when a factory is registered for <paramref name="type"/>.
    /// </summary>
    public static bool IsRegistered(Type type) => _factories.ContainsKey(type);

    /// <inheritdoc />
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (type is null || options is null)
            return null;

        if (!_factories.TryGetValue(type, out var factory))
            return null;

        var perOptions = _cache.GetValue(options, static _ => new ConcurrentDictionary<Type, JsonTypeInfo>());
        return perOptions.GetOrAdd(type, t => _factories[t](options));
    }
}

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Default <see cref="IRunContext"/>: a type-keyed bag over the trusted values supplied via
/// <c>WithContext(...)</c>. The framework builds one per agent run; you rarely construct it yourself.
/// Backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/> so concurrent tool calls can read and
/// <see cref="Set{T}"/> without corrupting the bag itself (the models it holds are yours to make
/// thread-safe — or not — as your design requires).
/// </summary>
/// <remarks>
/// Type-keyed lookups only — no reflection or serialization, so this is fully AOT/trim-safe.
/// </remarks>
public sealed class RunContext : IRunContext
{
    private readonly ConcurrentDictionary<Type, object> _values = new();

    /// <summary>Creates an empty context.</summary>
    public RunContext() { }

    /// <summary>Seeds the context from the caller-supplied values, keyed by each value's runtime type. Nulls are skipped.</summary>
    public RunContext(IEnumerable<object>? values)
    {
        if (values is null) return;
        foreach (var value in values)
            if (value is not null)
                _values[value.GetType()] = value;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<object> Values => _values.Values.ToArray();

    /// <inheritdoc />
    public T? Get<T>() where T : class => (T?)Resolve(typeof(T));

    /// <inheritdoc />
    public T GetRequired<T>() where T : class
        => Get<T>() ?? throw new AiToolContextException(
            $"No context value of type '{typeof(T).Name}' was supplied to this agent run. " +
            "Pass it via the agent/chat call's WithContext(...) argument.");

    /// <inheritdoc />
    public bool TryGet<T>([NotNullWhen(true)] out T? value) where T : class
    {
        value = Get<T>();
        return value is not null;
    }

    /// <inheritdoc />
    public bool Has<T>() where T : class => Resolve(typeof(T)) is not null;

    /// <inheritdoc />
    public void Set<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        _values[typeof(T)] = value;
    }

    // Exact runtime-type match wins (the common case); otherwise the single value assignable to
    // `type` (interface / base-class context). More than one assignable value is ambiguous.
    private object? Resolve(Type type)
    {
        if (_values.TryGetValue(type, out var exact))
            return exact;

        object? match = null;
        foreach (var value in _values.Values)
        {
            if (!type.IsInstanceOfType(value))
                continue;
            if (match is not null)
                throw new AiToolContextException(
                    $"Ambiguous context for type '{type.Name}': multiple supplied values are assignable to it. " +
                    "Pass a single matching value, or read a more specific type.");
            match = value;
        }
        return match;
    }
}

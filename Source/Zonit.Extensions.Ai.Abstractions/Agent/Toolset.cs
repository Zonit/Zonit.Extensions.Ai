using System.Collections;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Type-safe, <c>typeof</c>-free way to declare a sub-agent's <see cref="IAgent.Tools"/>.
/// Each generic argument is constrained to <see cref="ITool"/>, so a wrong type is a compile
/// error rather than a runtime surprise.
/// </summary>
/// <example>
/// <code>
/// // Fixed arity (up to six):
/// public override IReadOnlyList&lt;Type&gt; Tools => Toolset.Of&lt;GenerateLinkTool, ContactSaveTool&gt;();
///
/// // Unbounded fluent chain — add as many as you like:
/// public override IReadOnlyList&lt;Type&gt; Tools => Toolset.Add&lt;GenerateLinkTool&gt;().Add&lt;ContactSaveTool&gt;().Add&lt;PriceFeedTool&gt;();
/// </code>
/// </example>
/// <remarks>
/// Returns an <see cref="IReadOnlyList{Type}"/> so it drops straight into <see cref="IAgent.Tools"/>.
/// Use <see cref="Of{T1}()"/> overloads for one to six tools, or the fluent
/// <see cref="Add{T}()"/> chain for an arbitrary number — both are <c>typeof</c>-free and compile-checked.
/// </remarks>
public static class Toolset
{
    /// <summary>An empty tool set (the default for an agent that uses no tools).</summary>
    public static IReadOnlyList<Type> None { get; } = [];

    /// <summary>
    /// Starts a fluent, <b>unbounded</b> tool chain with one tool; keep appending with
    /// <see cref="ToolsetBuilder.Add{T}()"/>. The result is an <see cref="IReadOnlyList{Type}"/>,
    /// so it drops straight into <see cref="IAgent.Tools"/>.
    /// </summary>
    /// <example>
    /// <code>Toolset.Add&lt;A&gt;().Add&lt;B&gt;().Add&lt;C&gt;()</code>
    /// </example>
    public static ToolsetBuilder Add<T>()
        where T : class, ITool
        => new([typeof(T)]);

    /// <summary>Declares one tool.</summary>
    public static IReadOnlyList<Type> Of<T1>()
        where T1 : class, ITool
        => [typeof(T1)];

    /// <summary>Declares two tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2>()
        where T1 : class, ITool
        where T2 : class, ITool
        => [typeof(T1), typeof(T2)];

    /// <summary>Declares three tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2, T3>()
        where T1 : class, ITool
        where T2 : class, ITool
        where T3 : class, ITool
        => [typeof(T1), typeof(T2), typeof(T3)];

    /// <summary>Declares four tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2, T3, T4>()
        where T1 : class, ITool
        where T2 : class, ITool
        where T3 : class, ITool
        where T4 : class, ITool
        => [typeof(T1), typeof(T2), typeof(T3), typeof(T4)];

    /// <summary>Declares five tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2, T3, T4, T5>()
        where T1 : class, ITool
        where T2 : class, ITool
        where T3 : class, ITool
        where T4 : class, ITool
        where T5 : class, ITool
        => [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)];

    /// <summary>Declares six tools.</summary>
    public static IReadOnlyList<Type> Of<T1, T2, T3, T4, T5, T6>()
        where T1 : class, ITool
        where T2 : class, ITool
        where T3 : class, ITool
        where T4 : class, ITool
        where T5 : class, ITool
        where T6 : class, ITool
        => [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)];
}

/// <summary>
/// An immutable, chainable tool list produced by <see cref="Toolset.Add{T}()"/>. Each
/// <see cref="Add{T}()"/> returns a <b>new</b> builder with the extra tool appended, so an arbitrary
/// number of tools can be declared without a fixed-arity overload. Implements
/// <see cref="IReadOnlyList{Type}"/>, so it assigns directly to <see cref="IAgent.Tools"/>.
/// </summary>
/// <remarks>
/// Each generic argument is constrained to <see cref="ITool"/> (a wrong type is a compile error), and
/// the chain only collects <c>typeof</c>s — no reflection, AOT-clean. Build once in the property getter;
/// because every <see cref="Add{T}()"/> allocates a fresh array, reuse the resulting list rather than
/// re-chaining in a hot path.
/// </remarks>
public sealed class ToolsetBuilder : IReadOnlyList<Type>
{
    private readonly Type[] _types;

    internal ToolsetBuilder(Type[] types) => _types = types;

    /// <summary>Appends one tool and returns a new builder, so calls can be chained indefinitely.</summary>
    public ToolsetBuilder Add<T>()
        where T : class, ITool
    {
        var next = new Type[_types.Length + 1];
        Array.Copy(_types, next, _types.Length);
        next[_types.Length] = typeof(T);
        return new ToolsetBuilder(next);
    }

    /// <inheritdoc />
    public Type this[int index] => _types[index];

    /// <inheritdoc />
    public int Count => _types.Length;

    /// <inheritdoc />
    public IEnumerator<Type> GetEnumerator() => ((IEnumerable<Type>)_types).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _types.GetEnumerator();
}
